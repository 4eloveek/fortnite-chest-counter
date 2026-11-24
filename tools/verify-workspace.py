"""Independently verify the full workspace ZIP and optionally extract it for a clean build."""

import argparse
import hashlib
import json
from pathlib import Path, PurePosixPath
from zipfile import ZipFile


def verify(archive: Path, extract_to: Path | None) -> None:
    root_name = "fortnite-chest-counter"
    required = {
        "START-HERE.md", "README.md", "AGENTS.md", "FortniteChestCounter.sln",
        "Directory.Build.props", "global.json", "NuGet.Config",
        "Run.cmd", "Build.cmd", "Test.cmd", "Diagnostics.cmd", "Package.cmd",
        "src/FortniteChestCounter.Core/CounterLedger.cs",
        "src/FortniteChestCounter.Core/NotificationClassifier.cs",
        "src/FortniteChestCounter.Core/NotificationGate.cs",
        "src/FortniteChestCounter.Windows/MainForm.cs",
        "src/FortniteChestCounter.Windows/OcrReader.cs",
        "src/FortniteChestCounter.Windows/packages.lock.json",
        "tests/FortniteChestCounter.Tests/NotificationTests.cs",
        "tests/FortniteChestCounter.Tests/StorageTests.cs",
        "tests/FortniteChestCounter.Tests/packages.lock.json",
        "assets/ocr-models.json", "assets/nuget/packages.json",
        "assets/tessdata/eng.traineddata", "assets/tessdata/rus.traineddata",
        "assets/samples/README.md",
        "docs/PROJECT-CONTEXT.md", "docs/DESIGN-DECISIONS.md",
        "docs/WINDOWS-DEVELOPMENT.md", "docs/TROUBLESHOOTING.md", "docs/VERIFICATION.md",
        "tools/common.ps1", "tools/build.ps1", "tools/test.ps1", "tools/diagnose.ps1",
        "tools/collect-dependencies.ps1", "tools/package-workspace.ps1",
        "dist/FortniteChestCounter/FortniteChestCounter.exe",
        "dist/FortniteChestCounter/FortniteChestCounter.runtimeconfig.json",
        "dist/FortniteChestCounter/hostfxr.dll", "dist/FortniteChestCounter/coreclr.dll",
        "dist/FortniteChestCounter/System.Windows.Forms.dll",
        "dist/FortniteChestCounter/x64/tesseract50.dll",
        "dist/FortniteChestCounter/x64/leptonica-1.82.0.dll",
        "dist/FortniteChestCounter/tessdata/eng.traineddata",
        "dist/FortniteChestCounter/tessdata/rus.traineddata",
    }
    excluded = {"bin", "obj", ".packages", ".git", ".vs", "__pycache__", "diagnostics", "TestResults"}

    with ZipFile(archive) as workspace:
        names = workspace.namelist()
        if len(names) != len(set(names)):
            raise ValueError("Duplicate ZIP entries")
        for name in names:
            parts = PurePosixPath(name).parts
            if "\\" in name or not parts or parts[0] != root_name or ".." in parts:
                raise ValueError(f"Invalid workspace path: {name}")
            if any(part in excluded for part in parts[1:-1]):
                raise ValueError(f"Build cache unexpectedly bundled: {name}")
            if name.endswith(".zip"):
                raise ValueError(f"Nested release archive: {name}")

        manifest = json.loads(workspace.read(f"{root_name}/workspace-manifest.json"))
        if manifest["schemaVersion"] != 1 or manifest["rootFolder"] != root_name:
            raise ValueError("Unsupported workspace manifest")
        records = manifest["files"]
        indexed = {record["path"] for record in records}
        if len(indexed) != len(records) or len(names) != len(records) + 1:
            raise ValueError("Workspace manifest entry mismatch")
        missing = required - indexed
        if missing:
            raise ValueError(f"Missing workspace files: {', '.join(sorted(missing))}")

        hashes: dict[str, str] = {}
        for record in records:
            data = workspace.read(f"{root_name}/{record['path']}")  # Reading validates ZIP CRC too.
            digest = hashlib.sha256(data).hexdigest()
            if len(data) != record["bytes"] or digest != record["sha256"]:
                raise ValueError(f"Workspace checksum mismatch: {record['path']}")
            hashes[record["path"]] = digest
            if record["path"] in {"Run.cmd", "Build.cmd", "Test.cmd", "Diagnostics.cmd", "Package.cmd"}:
                data.decode("ascii")
                if b"\n" in data.replace(b"\r\n", b""):
                    raise ValueError(f"Batch file does not have Windows CRLF: {record['path']}")

        packages = json.loads(workspace.read(f"{root_name}/assets/nuget/packages.json"))["packages"]
        for package in packages:
            name = f"assets/nuget/{package['file']}"
            if hashes.get(name) != package["sha256"]:
                raise ValueError(f"Bundled NuGet checksum mismatch: {name}")
        models = json.loads(workspace.read(f"{root_name}/assets/ocr-models.json"))["models"]
        for model in models:
            for prefix in ("assets/tessdata", "dist/FortniteChestCounter/tessdata"):
                name = f"{prefix}/{model['id']}.traineddata"
                if hashes.get(name) != model["sha256"]:
                    raise ValueError(f"OCR model checksum mismatch: {name}")

        if extract_to is not None:
            if extract_to.exists() and any(extract_to.iterdir()):
                raise ValueError(f"Extraction destination is not empty: {extract_to}")
            extract_to.mkdir(parents=True, exist_ok=True)
            workspace.extractall(extract_to)
            print(f"Extracted: {extract_to / root_name}")

    print(f"Verified: {archive}")
    print(f"One root folder, {len(names)} entries, {len(packages)} NuGet packages; all SHA256 and CRC verified.")
    print("Windows batch CRLF and both copies of each OCR model verified.")


def main() -> None:
    parser = argparse.ArgumentParser(description=__doc__)
    root = Path(__file__).resolve().parent.parent
    parser.add_argument("--archive", type=Path, default=root / "FortniteChestCounter-Workspace.zip")
    parser.add_argument("--extract-to", type=Path)
    args = parser.parse_args()
    verify(args.archive, args.extract_to)


if __name__ == "__main__":
    main()
