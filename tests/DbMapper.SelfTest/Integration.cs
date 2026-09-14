using System.Diagnostics;
using System.Text.Json;
using DbMapper;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration.UserSecrets;

internal static partial class Program
{
    private static async Task Integration()
    {
        var adminSecret = Environment.GetEnvironmentVariable("DBMAPPER_TEST_ADMIN");
        Check(!string.IsNullOrWhiteSpace(adminSecret), "integration admin connection supplied by disposable-container script");
        var adminBuilder = new SqlConnectionStringBuilder(adminSecret);
        Check(adminBuilder.DataSource.StartsWith("127.0.0.1,", StringComparison.Ordinal) && adminBuilder.InitialCatalog == "master", "integration restricted to loopback and explicit master connection");
        var database = "DbMapperTest_" + Guid.NewGuid().ToString("N");
        var password = "Test!aA1" + Guid.NewGuid().ToString("N");
        var secretId = "dbmapper-selftest-" + Guid.NewGuid().ToString("N");
        var temp = NewTemp();
        await using var admin = new SqlConnection(adminSecret);
        for (var attempt = 0; ; attempt++)
        {
            try { await admin.OpenAsync(); break; }
            catch (SqlException) when (attempt < 60)
            {
                if (attempt % 10 == 0) Console.WriteLine("Waiting for disposable SQL Server startup...");
                await Task.Delay(1000);
            }
        }
        var created = false;
        try
        {
            await Execute(admin, $"CREATE DATABASE [{database}];");
            created = true;
            await Execute(admin, $"CREATE LOGIN [dbmapper_test_reader] WITH PASSWORD = '{password}', CHECK_POLICY = OFF; CREATE LOGIN [dbmapper_test_blind] WITH PASSWORD = '{password}', CHECK_POLICY = OFF;");
            var fixtureBuilder = new SqlConnectionStringBuilder(adminSecret) { InitialCatalog = database };
            await using (var fixture = new SqlConnection(fixtureBuilder.ConnectionString))
            {
                await fixture.OpenAsync();
                await Execute(fixture, "CREATE SCHEMA [sales / ü];");
                await Execute(fixture, """
                    CREATE TABLE dbo.Customers (
                        TenantId int NOT NULL,
                        Id int NOT NULL,
                        Email nvarchar(100) NULL,
                        Secret nvarchar(200) NOT NULL CONSTRAINT DF_Customers_Secret DEFAULT N'PRIVATE_DEFAULT_SENTINEL',
                        CONSTRAINT PK_Customers PRIMARY KEY (TenantId, Id),
                        CONSTRAINT UQ_Customers_Email UNIQUE (TenantId, Email),
                        CONSTRAINT CK_Customers_Secret CHECK (Secret <> N'PRIVATE_CHECK_SENTINEL')
                    );
                    CREATE TABLE [sales / ü].Orders (
                        Id bigint IDENTITY(1, 1) NOT NULL PRIMARY KEY,
                        TenantId int NOT NULL,
                        CustomerId int NOT NULL,
                        Amount decimal(18,2) NOT NULL,
                        CreatedAt datetime2(3) NOT NULL DEFAULT sysutcdatetime(),
                        Note nvarchar(max) NULL,
                        [odd|<name>] int NULL,
                        Calculated AS CONVERT(nvarchar(80), N'PRIVATE_COMPUTED_SENTINEL') PERSISTED,
                        CONSTRAINT FK_Orders_Customers FOREIGN KEY (TenantId, CustomerId)
                            REFERENCES dbo.Customers (TenantId, Id) ON DELETE CASCADE
                    );
                    CREATE NONCLUSTERED INDEX IX_Orders_Customer
                        ON [sales / ü].Orders (TenantId ASC, CustomerId DESC) INCLUDE (Amount)
                        WHERE Note <> N'PRIVATE_FILTER_SENTINEL';
                    INSERT dbo.Customers (TenantId, Id, Email) VALUES (1, 7, N'PRIVATE_ROW_SENTINEL');
                    INSERT [sales / ü].Orders (TenantId, CustomerId, Amount, Note) VALUES (1, 7, 1.25, N'PRIVATE_ORDER_SENTINEL');
                    EXEC sys.sp_addextendedproperty @name=N'MS_Description', @value=N'PRIVATE_PROPERTY_SENTINEL',
                        @level0type=N'SCHEMA', @level0name=N'dbo', @level1type=N'TABLE', @level1name=N'Customers';
                    """);
                await Execute(fixture, "CREATE VIEW dbo.CustomerView AS SELECT TenantId, Id, N'PRIVATE_VIEW_SENTINEL' AS Constant FROM dbo.Customers;");
                await Execute(fixture, "CREATE PROCEDURE dbo.FindCustomer @TenantId int, @Hint nvarchar(20) = N'PRIVATE_PARAM', @Count int OUTPUT AS SELECT @Count = 1; SELECT N'PRIVATE_PROCEDURE_SENTINEL';");
                await Execute(fixture, "CREATE FUNCTION dbo.ScalarValue(@Amount decimal(18,2)) RETURNS decimal(18,2) AS BEGIN RETURN @Amount; END;");
                await Execute(fixture, "CREATE TRIGGER dbo.CustomerGuard ON dbo.Customers AFTER UPDATE AS BEGIN SET NOCOUNT ON; DECLARE @Secret nvarchar(100) = N'PRIVATE_TRIGGER_SENTINEL'; END;");
                await Execute(fixture, """
                    CREATE USER [dbmapper_test_reader] FOR LOGIN [dbmapper_test_reader];
                    GRANT CONNECT TO [dbmapper_test_reader];
                    GRANT VIEW DEFINITION TO [dbmapper_test_reader];
                    DENY SELECT, INSERT, UPDATE, DELETE, EXECUTE ON SCHEMA::dbo TO [dbmapper_test_reader];
                    DENY SELECT, INSERT, UPDATE, DELETE, EXECUTE ON SCHEMA::[sales / ü] TO [dbmapper_test_reader];
                    CREATE USER [dbmapper_test_blind] FOR LOGIN [dbmapper_test_blind];
                    GRANT CONNECT TO [dbmapper_test_blind];
                    """);
            }
            await Execute(admin, $"ALTER DATABASE [{database}] SET READ_ONLY WITH ROLLBACK IMMEDIATE;");
            var readerBuilder = new SqlConnectionStringBuilder(adminSecret)
            {
                InitialCatalog = database,
                UserID = "dbmapper_test_reader",
                Password = password,
                Pooling = false,
                ConnectTimeout = 5
            };
            await using (var denied = new SqlConnection(readerBuilder.ConnectionString))
            {
                await denied.OpenAsync();
                foreach (var sql in new[] { "SELECT * FROM dbo.Customers;", "SELECT * FROM dbo.CustomerView;", "SELECT * FROM [sales / ü].Orders;", "EXEC dbo.FindCustomer 1, NULL, NULL;" })
                {
                    try { await Execute(denied, sql); throw new CheckFailure("FAIL: metadata login can read rows or execute user code"); }
                    catch (SqlException e) when (e.Number == 229) { Check(true, "application row/code access denied"); }
                }
            }
            Console.WriteLine("Testing catalog extraction with denied row access and a read-only fixture database...");
            var model = await CatalogReader.ReadAsync(readerBuilder.ConnectionString, 10, CancellationToken.None);
            Check(model.Relations.Count == 3 && model.Routines.Count == 2, "real table/view/routine coverage");
            var customers = model.Relations.Single(r => r.Name == "Customers");
            var orders = model.Relations.Single(r => r.Name == "Orders");
            Check(customers.Columns.Single(c => c.Name == "Secret").HasDefault, "default presence without value");
            Check(orders.Columns.Single(c => c.Name == "Amount").Type == "decimal(18,2)", "decimal precision and scale");
            Check(orders.Columns.Single(c => c.Name == "CreatedAt").Type == "datetime2(3)", "temporal precision");
            Check(orders.Columns.Single(c => c.Name == "Note").Type == "nvarchar(max)", "max-length type");
            Check(orders.Columns.Single(c => c.Name == "Calculated").Computed, "computed flag");
            Check(orders.Columns.Single(c => c.Name == "Id").Identity, "identity flag");
            var index = orders.Indexes.Single(i => i.Name == "IX_Orders_Customer");
            Check(index.Filtered && index.Columns.Single(c => c.Name == "CustomerId").Descending && index.Columns.Single(c => c.Name == "Amount").Included, "filtered index, direction and include metadata");
            Check(customers.Indexes.Any(i => i.UniqueConstraint), "unique constraint metadata");
            var key = orders.ForeignKeys.Single();
            Check(key.OnDelete == "CASCADE" && key.Columns.OrderBy(c => c.Ordinal).Select(c => c.Source).SequenceEqual(["TenantId", "CustomerId"]), "composite foreign key order and action");
            Check(customers.Triggers.Count == 1 && customers.Checks.Count == 1, "trigger and check flags");
            Check(model.Routines.Single(r => r.Name == "FindCustomer").Parameters.Single(p => p.Name == "@Count").Output, "output parameter metadata");
            Check(model.Routines.Single(r => r.Name == "ScalarValue").Parameters.Any(p => p.Ordinal == 0 && p.Type == "decimal(18,2)"), "scalar return metadata");
            var files = OkfBundle.Render(model);
            Check(files.OrderBy(p => p.Key).SequenceEqual(OkfBundle.Render(SqlModelReader.Read(files)).OrderBy(p => p.Key)), "existing live catalog metadata round trips for offline updates");
            ValidateBundle(files);
            var all = string.Join("\n", files.Values.Concat(files.Keys));
            foreach (var forbidden in new[] { "PRIVATE_", database, password, "127.0.0.1", "dbmapper_test_reader", "User ID=" })
                Check(!all.Contains(forbidden, StringComparison.Ordinal), "sensitive values excluded from bundle");

            var project = Path.Combine(temp, "App.csproj");
            File.WriteAllText(project, $"<Project Sdk=\"Microsoft.NET.Sdk\"><PropertyGroup><TargetFramework>net8.0</TargetFramework><UserSecretsId>{secretId}</UserSecretsId></PropertyGroup></Project>");
            Check(await SetSecrets(secretId, new() { ["ConnectionStrings:Metadata"] = readerBuilder.ConnectionString, ["Unrelated:Secret"] = "PRIVATE_UNRELATED_SENTINEL" }) == 0, "standard dotnet user-secret setup");
            var output = Path.Combine(temp, "cli-bundle");
            var stdout = new StringWriter();
            var stderr = new StringWriter();
            var arguments = new[] { "--project", project, "--output", output };
            Check(await Cli.RunAsync(arguments, stdout, stderr) == 0, "real CLI from existing project user secrets");
            var first = ReadBundle(output);
            var packagedTool = Environment.GetEnvironmentVariable("DBMAPPER_TEST_TOOL");
            if (!string.IsNullOrEmpty(packagedTool))
            {
                var packagedOutput = Path.Combine(temp, "packaged-bundle");
                var start = new ProcessStartInfo(packagedTool) { UseShellExecute = false, RedirectStandardOutput = true, RedirectStandardError = true, CreateNoWindow = true, WorkingDirectory = temp };
                foreach (var argument in new[] { "--output", packagedOutput }) start.ArgumentList.Add(argument);
                using var process = Process.Start(start)!;
                var toolOut = process.StandardOutput.ReadToEndAsync();
                var toolError = process.StandardError.ReadToEndAsync();
                await process.WaitForExitAsync();
                var diagnostics = await toolOut + await toolError;
                Check(process.ExitCode == 0 && diagnostics.Contains("OKF v0.2", StringComparison.Ordinal), "installed package command runs inside existing dotnet project");
                Check(first.OrderBy(x => x.Key).SequenceEqual(ReadBundle(packagedOutput).OrderBy(x => x.Key)), "installed package exports identical bundle");
                Check(!diagnostics.Contains(password, StringComparison.Ordinal) && !diagnostics.Contains(database, StringComparison.Ordinal), "installed package diagnostics sanitized");
            }
            Check(await Cli.RunAsync(arguments, stdout, stderr) == 0, "real CLI refresh");
            Check(first.OrderBy(x => x.Key).SequenceEqual(ReadBundle(output).OrderBy(x => x.Key)), "live refresh identical bytes");
            Check(!Directory.GetFiles(temp, "*", SearchOption.AllDirectories).Any(p => File.ReadAllText(p).Contains(password, StringComparison.Ordinal)), "no connection secret copied to project");

            var wrongPassword = new SqlConnectionStringBuilder(readerBuilder.ConnectionString) { Password = "PRIVATE_BAD_PASSWORD" };
            Check(await SetSecrets(secretId, new() { ["ConnectionStrings:Metadata"] = wrongPassword.ConnectionString }) == 0, "failure fixture secret update");
            Check(await Cli.RunAsync(arguments, stdout, stderr) == 3, "sanitized authentication failure");
            Check(first.OrderBy(x => x.Key).SequenceEqual(ReadBundle(output).OrderBy(x => x.Key)), "failed connection preserves bundle");
            var blind = new SqlConnectionStringBuilder(readerBuilder.ConnectionString) { UserID = "dbmapper_test_blind" };
            Check(await SetSecrets(secretId, new() { ["ConnectionStrings:Metadata"] = blind.ConnectionString }) == 0, "visibility fixture update");
            Check(await Cli.RunAsync(arguments, stdout, stderr) == 2 && stderr.ToString().Contains("VIEW DEFINITION", StringComparison.Ordinal), "missing metadata permission refused");
            Check(await SetSecrets(secretId, new() { ["ConnectionStrings:Metadata"] = readerBuilder.ConnectionString }) == 0, "restore metadata fixture secret");
            using var cancellation = new CancellationTokenSource();
            cancellation.Cancel();
            Check(await Cli.RunAsync(arguments, stdout, stderr, cancellation.Token) == 130, "cancellation exit");
            foreach (var forbidden in new[] { "PRIVATE_", database, password, "127.0.0.1", secretId, "dbmapper_test_reader" })
                Check(!(stdout.ToString() + stderr.ToString()).Contains(forbidden, StringComparison.Ordinal), "diagnostics redact secrets and endpoints");
            File.AppendAllText(Path.Combine(output, "database.md"), "Human note");
            Check(await Cli.RunAsync(arguments, stdout, stderr) == 2, "live edited output protection");

            await using var verify = new SqlConnection(new SqlConnectionStringBuilder(adminSecret) { InitialCatalog = database }.ConnectionString);
            await verify.OpenAsync();
            await using var count = verify.CreateCommand();
            count.CommandText = "SELECT COUNT(*) FROM dbo.Customers WHERE Email = N'PRIVATE_ROW_SENTINEL';";
            Check(Convert.ToInt32(await count.ExecuteScalarAsync()) == 1, "fixture application data unchanged");
            await OfflineSqlIntegration(admin, adminSecret!, temp);
            await ServerIntegration(admin, adminSecret!, readerBuilder.ConnectionString, database, project, secretId, temp);
        }
        finally
        {
            await SecretsProcess(["user-secrets", "clear", "--id", secretId], null);
            var secretsPath = Path.GetFullPath(PathHelper.GetSecretsPathFromSecretsId(secretId));
            var secretsDirectory = Path.GetDirectoryName(secretsPath)!;
            // Remove only this invocation's synthetic store; never enumerate or inspect other stores.
            if (Path.GetFileName(secretsDirectory) == secretId && secretId.StartsWith("dbmapper-selftest-", StringComparison.Ordinal))
            {
                File.Delete(secretsPath);
                if (Directory.Exists(secretsDirectory)) Directory.Delete(secretsDirectory, recursive: false);
            }
            DeleteTemp(temp);
            if (created) await Execute(admin, $"ALTER DATABASE [{database}] SET SINGLE_USER WITH ROLLBACK IMMEDIATE; DROP DATABASE [{database}];");
            await Execute(admin, "IF EXISTS (SELECT 1 FROM sys.server_principals WHERE name = 'dbmapper_test_reader') DROP LOGIN [dbmapper_test_reader]; IF EXISTS (SELECT 1 FROM sys.server_principals WHERE name = 'dbmapper_test_blind') DROP LOGIN [dbmapper_test_blind];");
        }
    }

    private static async Task Execute(SqlConnection connection, string sql)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        command.CommandTimeout = 30;
        await command.ExecuteNonQueryAsync();
    }

    private static Task<int> SetSecrets(string id, Dictionary<string, string> values) =>
        SecretsProcess(["user-secrets", "set", "--id", id], JsonSerializer.Serialize(values));

    private static async Task<int> SecretsProcess(string[] arguments, string? input)
    {
        var start = new ProcessStartInfo("dotnet") { UseShellExecute = false, RedirectStandardInput = true, RedirectStandardOutput = true, RedirectStandardError = true, CreateNoWindow = true };
        foreach (var argument in arguments) start.ArgumentList.Add(argument);
        using var process = Process.Start(start)!;
        var stdout = process.StandardOutput.ReadToEndAsync();
        var stderr = process.StandardError.ReadToEndAsync();
        if (input is not null) await process.StandardInput.WriteAsync(input);
        process.StandardInput.Close();
        await process.WaitForExitAsync();
        await Task.WhenAll(stdout, stderr); // Deliberately never echo secret-management output.
        return process.ExitCode;
    }
}
