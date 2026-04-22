#!/usr/bin/env python3
"""
.NET SAST Docker - Delivery Package Creator

Usage:
  python pack_delivery.py                          # scripts only (~14 KB)
  python pack_delivery.py --include-image          # scripts + tar.gz (~510 MB)
  python pack_delivery.py --output-dir C:\\Shares  # custom output path
  python pack_delivery.py --name dotnet-sast-v2.0  # custom zip name
"""

import argparse
import os
import sys
import zipfile
from pathlib import Path


def fmt_size(n_bytes: int) -> str:
    if n_bytes >= 1_073_741_824:
        return f"{n_bytes / 1_073_741_824:.1f} GB"
    if n_bytes >= 1_048_576:
        return f"{n_bytes / 1_048_576:.0f} MB"
    return f"{n_bytes / 1024:.1f} KB"


def add_file(zf: zipfile.ZipFile, src: Path, dst: str, large: bool = False) -> bool:
    if not src.exists():
        print(f"  [WARN] Not found, skipping: {src}", file=sys.stderr)
        return False
    size = src.stat().st_size
    compression = zipfile.ZIP_STORED if large else zipfile.ZIP_DEFLATED
    print(f"  [ADD]  {dst}  ({fmt_size(size)})", end="", flush=True)
    if large:
        print("  <- copying (already compressed, no re-compression)...", end="", flush=True)
    zf.write(src, dst, compress_type=compression)
    print("  done")
    return True


def main() -> None:
    parser = argparse.ArgumentParser(description=".NET SAST Docker delivery packager")
    parser.add_argument("--output-dir", default=str(Path.home() / "Desktop"),
                        help="Output directory (default: Desktop)")
    parser.add_argument("--name", default="dotnet-sast-docker-v1.0",
                        help="ZIP file name without extension")
    parser.add_argument("--include-image", action="store_true",
                        help="Bundle dotnet-sast-scanner.tar.gz into the ZIP (~510 MB total)")
    args = parser.parse_args()

    script_dir = Path(__file__).parent       # docker/
    skill_root = script_dir.parent           # dotnet-sast-pipeline/
    output_dir = Path(args.output_dir)
    zip_path   = output_dir / f"{args.name}.zip"

    tar_gz = script_dir / "dotnet-sast-scanner.tar.gz"

    if args.include_image and not tar_gz.exists():
        print(f"[ERROR] tar.gz not found: {tar_gz}", file=sys.stderr)
        print("  Run first: docker save dotnet-sast-scanner:latest | gzip > docker/dotnet-sast-scanner.tar.gz")
        sys.exit(1)

    print("=" * 55)
    mode = "WITH Docker image (~510 MB)" if args.include_image else "scripts only (~14 KB)"
    print(f"  .NET SAST Docker — Packaging ({mode})")
    print("=" * 55)

    output_dir.mkdir(parents=True, exist_ok=True)

    # ZIP_DEFLATED for scripts, ZIP_STORED for already-compressed tar.gz
    with zipfile.ZipFile(zip_path, "w") as zf:
        core_files = [
            (script_dir / "Dockerfile",         "Dockerfile"),
            (script_dir / "entrypoint.sh",      "entrypoint.sh"),
            (script_dir / "scan.ps1",           "scan.ps1"),
            (script_dir / "docker-compose.yml", "docker-compose.yml"),
            (script_dir / ".dockerignore",      ".dockerignore"),
            (script_dir / "HOWTO.txt",          "HOWTO.txt"),
            (skill_root / "scripts" / "generate_report.py", "scripts/generate_report.py"),
            (skill_root / "references" / "dotnet-vuln-patterns.md", "references/dotnet-vuln-patterns.md"),
        ]
        for src, dst in core_files:
            add_file(zf, src, dst, large=False)

        if args.include_image:
            add_file(zf, tar_gz, "dotnet-sast-scanner.tar.gz", large=True)

    total = zip_path.stat().st_size
    print()
    print("=" * 55)
    print("Package ready!")
    print(f"  File : {zip_path}")
    print(f"  Size : {fmt_size(total)}")
    print()

    if args.include_image:
        print("Colleague's steps (NO internet required):")
        print("  1. Install Docker Desktop")
        print("  2. Extract ZIP")
        print("  3. Run: .\\scan.ps1 -ProjectPath <path>")
        print("     (auto-detects tar.gz and loads image, ~1-3 min)")
    else:
        print("Colleague's steps (internet required for first build):")
        print("  1. Install Docker Desktop")
        print("  2. Extract ZIP")
        print("  3. First run: .\\scan.ps1 -ProjectPath <path> -Build  (~5-10 min)")
        print("  4. Next runs: .\\scan.ps1 -ProjectPath <path>")

    print()
    print("  Report: <project-path>\\sast-output\\security-report.md")


if __name__ == "__main__":
    main()
