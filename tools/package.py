"""Package the published Windows app and verify the archive contents."""

from pathlib import Path
from zipfile import ZIP_DEFLATED, ZipFile


def main() -> None:
    root = Path(__file__).resolve().parent.parent
    release = root / "dist" / "FortniteChestCounter"
    archive = root / "FortniteChestCounter-Windows-x64.zip"
    required = (
        "FortniteChestCounter.exe",
        "FortniteChestCounter.dll",
        "hostfxr.dll",
        "coreclr.dll",
        "System.Windows.Forms.dll",
        "x64/tesseract50.dll",
        "x64/leptonica-1.82.0.dll",
        "tessdata/eng.traineddata",
        "tessdata/rus.traineddata",
        "licenses/Tesseract-Apache-2.0.txt",
        "licenses/Leptonica-BSD.txt",
        "licenses/dotnet-MIT.txt",
        "README.md",
        "THIRD-PARTY-NOTICES.md",
    )
    missing = [name for name in required if not (release / name).is_file()]
    if missing:
        raise SystemExit(f"Missing published files: {', '.join(missing)}")

    with ZipFile(archive, "w", ZIP_DEFLATED, compresslevel=6) as output:
        for path in sorted(release.rglob("*")):
            if path.is_file() and path.suffix != ".pdb":
                output.write(path, Path(release.name) / path.relative_to(release))

    with ZipFile(archive) as result:
        invalid = result.testzip()
        if invalid:
            raise SystemExit(f"Archive CRC check failed: {invalid}")
        for name in required:
            result.getinfo(f"{release.name}/{name}")
        count = len(result.infolist())

    print(f"Ready: {archive}")
    print(f"{count} files, {archive.stat().st_size / 1024 / 1024:.1f} MiB; CRC verified.")


if __name__ == "__main__":
    main()
