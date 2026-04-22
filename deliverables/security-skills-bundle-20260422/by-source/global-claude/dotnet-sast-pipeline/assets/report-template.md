# .NET SAST 安全掃描報告

> 產生時間：{{SCAN_DATE}}
> 掃描目標：`{{TARGET_PATH}}`
> 總體風險：{{RISK_LEVEL}}

---

## 執行摘要

| 嚴重程度 | 數量 |
|----------|------|
| 🔴 Critical | {{COUNT_CRITICAL}} |
| 🟠 High     | {{COUNT_HIGH}} |
| 🟡 Medium   | {{COUNT_MEDIUM}} |
| 🔵 Low      | {{COUNT_LOW}} |
| **合計**    | **{{COUNT_TOTAL}}** |

### 工具執行狀態

| 工具 | 狀態 | 發現數 |
|------|------|--------|
| dotnet list package --vulnerable | {{STATUS_NUGET}} | {{COUNT_NUGET}} |
| Semgrep OSS | {{STATUS_SEMGREP}} | {{COUNT_SEMGREP}} |
| SecurityCodeScan | {{STATUS_SCS}} | {{COUNT_SCS}} |
| Gitleaks | {{STATUS_GITLEAKS}} | {{COUNT_GITLEAKS}} |

---

## 🔴 Critical 發現

### 1. {{FINDING_TITLE}}

- **工具**：{{TOOL}}
- **位置**：`{{FILE_PATH}}:{{LINE}}`
- **CWE**：{{CWE}}

> {{DESCRIPTION}}

```csharp
// 漏洞程式碼
{{VULNERABLE_CODE}}
```

**修補建議：**

{{REMEDIATION}}

---

## 🟠 High 發現

<!-- High 等級發現 -->

---

## 🟡 Medium 發現

<!-- Medium 等級發現 -->

---

## 🔵 Low 發現

<!-- Low 等級發現 -->

---

## 修補優先順序建議

1. **立即處理（本次 Sprint）**：所有 Critical + High
   - 密鑰洩漏（輪換憑證）
   - SQL Injection（改用參數化查詢）
   - 不安全反序列化（移除 BinaryFormatter）

2. **短期計劃（下個 Sprint）**：Medium
   - 弱加密（升級至 AES-256 / bcrypt）
   - CSRF 保護缺失（加 ValidateAntiForgeryToken）

3. **長期改善**：Low / Info
   - TLS 設定優化
   - 過時套件升級

---

*報告由 dotnet-sast-pipeline 自動產生*
