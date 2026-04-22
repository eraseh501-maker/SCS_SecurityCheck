# ─────────────────────────────────────────────────────────────
# .NET SAST Scanner — Docker Image
# 包含工具：Semgrep、SecurityCodeScan、Gitleaks、dotnet list package
#
# 建置：docker build -t dotnet-sast-scanner .
# 執行：docker run --rm -v "C:\Projects\MyApp:/scan" dotnet-sast-scanner
# ─────────────────────────────────────────────────────────────

# ── Stage 1：下載 Gitleaks（避免污染最終層）──
FROM debian:bookworm-slim AS gitleaks-downloader
ARG GITLEAKS_VERSION=8.18.4
RUN apt-get update && apt-get install -y --no-install-recommends wget ca-certificates \
    && wget -q \
       "https://github.com/gitleaks/gitleaks/releases/download/v${GITLEAKS_VERSION}/gitleaks_${GITLEAKS_VERSION}_linux_x64.tar.gz" \
       -O /tmp/gitleaks.tar.gz \
    && tar -xf /tmp/gitleaks.tar.gz -C /tmp gitleaks \
    && chmod +x /tmp/gitleaks

# ── Stage 2：主要映像 ──
FROM mcr.microsoft.com/dotnet/sdk:8.0

# 標籤
LABEL maintainer="dotnet-sast-pipeline" \
      description=".NET / C# SAST 安全掃描工具集" \
      version="1.0.0"

# ── 安裝系統相依套件 ──
RUN apt-get update && apt-get install -y --no-install-recommends \
        python3 \
        python3-pip \
        python3-venv \
        git \
        curl \
        ca-certificates \
    && rm -rf /var/lib/apt/lists/*

# ── 安裝 Python 工具：Semgrep ──
RUN pip3 install --no-cache-dir --break-system-packages semgrep

# ── 安裝 SecurityCodeScan CLI（dotnet global tool）──
RUN dotnet tool install --global security-scan --version 5.6.7 \
    || dotnet tool install --global security-scan
ENV PATH="${PATH}:/root/.dotnet/tools"

# ── 複製 Gitleaks binary ──
COPY --from=gitleaks-downloader /tmp/gitleaks /usr/local/bin/gitleaks

# ── 複製掃描腳本 ──
WORKDIR /sast-tools
COPY scripts/generate_report.py ./generate_report.py
COPY docker/entrypoint.sh ./entrypoint.sh
RUN chmod +x entrypoint.sh

# ── 驗證所有工具已就緒 ──
RUN echo "=== 工具版本驗證 ===" \
    && dotnet --version \
    && python3 --version \
    && semgrep --version \
    && gitleaks version \
    && security-scan --version 2>/dev/null || echo "security-scan: installed"

# 掛載點：同事的專案目錄
VOLUME ["/scan"]

ENTRYPOINT ["/sast-tools/entrypoint.sh"]
CMD []
