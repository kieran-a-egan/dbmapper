"""Independent checks for the OKF v0.2 subset emitted by DbMapper; not a general OKF validator."""

import hashlib
from pathlib import Path
import re
import sys

import yaml


def validate(directory: Path) -> None:
    root = directory.resolve(strict=True)
    files = list(root.rglob("*.md"))
    assert files, "Bundle has no Markdown files"
    for path in files:
        raw = path.read_bytes()
        assert b"\r" not in raw and not raw.startswith(b"\xef\xbb\xbf"), "Expected UTF-8 without BOM and LF newlines"
        text = raw.decode("utf-8", errors="strict")
        frontmatter = None
        if text.startswith("---\n"):
            _, header, _ = text.split("---\n", 2)
            frontmatter = yaml.safe_load(header)
            assert isinstance(frontmatter, dict), "Frontmatter must be a YAML mapping"
        if path == root / "index.md":
            assert frontmatter == {"okf_version": "0.2"}, "Root index must declare only the format version"
        elif path.name == "index.md":
            assert frontmatter is None, "Nested index must have no frontmatter"
        elif path.name == "log.md":
            raise AssertionError("DbMapper does not emit log.md")
        else:
            assert frontmatter and isinstance(frontmatter.get("type"), str) and frontmatter["type"].strip(), "Missing concept type"
            assert frontmatter["status"] == "draft", "Generated context must not claim human review"
            assert re.fullmatch(r"dbmapper/\d+\.\d+\.\d+", frontmatter["generated"]["by"]), "Missing producer identity"
            assert "verified" not in frontmatter, "No independent verification is claimed"
            assert all(isinstance(s.get("resource"), str) and s["resource"] for s in frontmatter["sources"]), "Missing source descriptor"
        for link in re.findall(r"\]\(([^)]+\.md)\)", text):
            target = (path.parent / link).resolve()
            assert target.is_relative_to(root) and target.is_file(), "Broken or out-of-bundle link"
        content, marker = text.rsplit("\n<!-- dbmapper:sha256=", 1)
        assert marker == hashlib.sha256(content.encode("utf-8")).hexdigest() + " -->\n", "Integrity footer mismatch"
    print(f"PASS: {len(files)} files have valid OKF v0.2 structure, YAML, links and integrity footers.")


if __name__ == "__main__":
    validate(Path(sys.argv[1]))
