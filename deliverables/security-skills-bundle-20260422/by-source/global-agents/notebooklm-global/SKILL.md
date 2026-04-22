---
name: notebooklm-global
description: >
  NotebookLM 全域自動化技能（繁體中文版）。適用所有專案，特別針對
  HR 系統合規開發流程。整合 Readwise 蒐集法規、NotebookLM 封閉知識庫問答、
  Obsidian 留存核查與 SRS 文件、Codex 串接全流程。觸發關鍵字：
  notebooklm, nlm, 合規核查, 勞基法, 勞健保, 所得稅, 內控辦法, audio overview,
  source add, studio create, research import, 合規報告。
allowed-tools: Read, Grep, Glob, Bash, WebFetch, Write
---

# NotebookLM 全域技能（繁中）

此技能為跨專案通用模板，重點用途為「法規驅動的 HR 功能開發與合規查驗」。

## 整合來源（obsidian-skills-main）

本技能已整合以下 Obsidian 技能能力，作為 HR 合規開發標準流程：

| 來源技能 | 在本流程中的用途 |
|---|---|
| `obsidian-markdown` | 產出結構一致的合規核查筆記、SRS、PR 合規複查報告 |
| `obsidian-bases` | 建立「功能-法條-內控-控制點」可篩選資料視圖 |
| `json-canvas` | 視覺化法規到控制點的追溯關係圖 |
| `obsidian-cli` | 批次建立/更新 Obsidian 文件與屬性，支援自動化工作流 |
| `defuddle` | 從法規網站抽取乾淨 Markdown，降低雜訊與 token 成本 |

## 觸發條件與執行模式

當使用者提到任一關鍵字時，優先啟用此技能流程：

- `HR系統`, `合規`, `勞基法`, `勞健保`, `所得稅`, `內控`, `SRS`, `Obsidian`, `NotebookLM`

執行模式：

1. `Bootstrap`：首次建立法規知識庫與 Obsidian 結構。
2. `Feature Check`：針對單一功能做開發前核查 + 開發後複查。
3. `Delta Review`：法規或內規更新後，增量更新控制點與報告。

## 四工具角色定義

| 工具 | 在流程中的角色 |
|---|---|
| Readwise | 收集外部法規來源（條文、函釋、修法公告） |
| NotebookLM | 將法規文件與內控辦法綁定為封閉知識庫，執行合規問答 |
| Obsidian | 儲存合規核查結果、需求決策紀錄、SRS 草稿 |
| Codex | 串接上述三者，執行開發與合規查驗流程 |

## Obsidian 標準輸出規格

所有輸出 Markdown 檔案應包含 frontmatter，最低欄位如下：

```yaml
---
title: 特休計算合規核查
module: leave
feature: annual_leave_auto_calc
status: draft
law_refs:
  - 勞基法第38條
policy_refs:
  - 飛騰薪資辦法第X條
risk_level: high
owner: HRIS
review_date: 2026-03-22
---
```

正文最少四段：

1. 適用法源與版本
2. 規格解讀與系統規則
3. 風險與例外情境
4. 控制點與驗證證據

## 內控對照 Base 檔模板

當使用者要求「結構化」或「內控對照表」時，建立 `.base`：

```yaml
filters:
  and:
    - 'file.inFolder("飛騰雲端HR系統/合規核查")'

formulas:
  has_gap: 'if(status != "closed" && risk_level == "high", true, false)'

properties:
  feature:
    displayName: 功能
  law_refs:
    displayName: 法條依據
  policy_refs:
    displayName: 內控依據
  risk_level:
    displayName: 風險等級
  formula.has_gap:
    displayName: 高風險未結案

views:
  - type: table
    name: 功能法遵總覽
    order:
      - file.name
      - feature
      - law_refs
      - policy_refs
      - risk_level
      - status
      - formula.has_gap
```

## 追溯圖 Canvas 模板

當使用者要求「可視化追溯」時，建立 `.canvas`，至少包含：

1. 功能節點（如：加班費模組）
2. 法條節點（如：勞基法第24、32、36條）
3. 內規節點（公司辦法條文）
4. 控制點節點（preventive/detective）
5. 邊線標註（依據/衝突/覆蓋）

## 工具選擇規則（MCP 與 CLI）

1. 先檢查是否可用 NotebookLM MCP 工具（例如 notebook_list、source_add、studio_create）。
2. 再檢查是否可用 CLI（執行 nlm --version）。
3. 若 MCP 與 CLI 皆可用，先詢問使用者偏好一次（MCP 或 CLI）。
4. 若僅有一種可用，直接走該路徑。

## 安全與可靠性規則

1. 認證狀態不明時，先執行 nlm login。
2. 刪除或不可逆操作前，必須取得明確確認。
3. 建立 Studio 內容前，先回報設定摘要，再使用 confirm 執行。
4. 發生 401/403 或 CSRF 錯誤，先重新登入再重試。
5. 合規輸出需標示法源依據與版本日期，避免無來源結論。

## Step 1：建立法規知識庫（Readwise）

### 操作原則

1. 建立專屬 Tag：台灣勞動法規、勞健保、所得稅、內控辦法。
2. 匯入來源：
   - 勞動部法規查詢系統條文（可用 Web Clipper）。
   - 內政部函釋 PDF（轉成 Readwise document）。
   - 公司內控制度與薪資辦法（標記為內控文件）。
3. 要求每份文件都具備：標題、來源網址或檔案名、版本日期、適用範圍。
4. 網頁來源先用 Defuddle 抽取正文，再匯入 Readwise，避免導覽列/廣告污染內容。

## Step 2：建立合規知識庫（NotebookLM）

NotebookLM 必須作為封閉知識庫，回答僅基於已匯入文件。

### 建庫任務範本

```text
建立一個 NotebookLM Notebook，命名為「HR系統合規核查庫」。
來源文件：
- 勞動基準法全文（從 Readwise 匯入）
- 勞工保險條例
- 全民健康保險法
- 就業保險法
- 所得稅法（薪資相關條文）
- 飛騰雲端內部控制制度辦法（人事循環章節）
- 飛騰雲端薪資作業辦法
```

### 建庫後動作

1. 生成 Audio Overview（Podcast）作為法規總覽。
2. 開發前先查詢衝突點，例如：

```text
加班費計算邏輯：勞基法第 24 條規定與公司內控辦法第 X 條之間是否有衝突？
```

## Step 3：開發流程嵌入合規查驗（Codex + Obsidian）

### 3a. 功能需求確認階段

每個 HR 功能開發前，先對 NotebookLM 進行合規提問，輸出到 Obsidian。

```text
我要開發「特休自動計算」功能。
請查詢 NotebookLM「HR系統合規核查庫」，列出：
1. 相關法條（勞基法第幾條）
2. 計算基準的法定規則
3. 公司內控辦法是否有額外規定
4. 潛在合規風險點
輸出格式：Markdown，存入 Obsidian /HR系統/合規核查/特休計算.md
```

補充規則：

1. 若輸出為規則型需求，需附上「可機器執行」欄位：`trigger`、`condition`、`calculation`、`exceptions`。
2. 若偵測到法條與內規衝突，需新增「衝突處理決策」段落並記錄決策者。

### 3b. 程式邏輯撰寫階段

程式碼需附法源與內控依據，確保可追溯。

```python
# 特休天數計算
# 法源：勞基法第 38 條
# 內控依據：飛騰薪資辦法第 X 條
def calculate_annual_leave(years_of_service: int) -> int:
    ...
```

### 3c. PR 或功能完成後的合規複查

```text
對剛完成的「加班費模組」進行合規自審：
1. 比對 NotebookLM 中勞基法第 24、32、36 條規定
2. 確認計算邏輯是否涵蓋所有法定例外情況
3. 輸出合規核查報告到 Obsidian /HR系統/合規報告/加班費模組_YYYYMMDD.md
```

報告最低內容：

1. 覆蓋條文清單（含條號）
2. 未覆蓋例外情境
3. 建議新增/調整控制點
4. 測試證據連結（測試案例、SQL、截圖、日誌）

## Step 4：cmux 多工管理

建議同時開多個 Codex session：

1. Session 1：開發薪資模組。
2. Session 2：同步查驗勞健保費率合規性。
3. Session 3：更新 Obsidian 合規紀錄與 SRS 文件。

## Obsidian 資料夾結構建議

```text
/飛騰雲端HR系統/
  /合規核查/
    特休計算.md
    加班費模組.md
    勞健保扣繳.md
  /合規報告/
    加班費模組_20260322.md
  /SRS/
    薪資模組_v1.md
  /內控對照表/
    功能_法條_內控辦法對照.md
```

## CLI 快速指令（常用）

```bash
nlm --version
nlm login
nlm notebook list
nlm notebook create "HR系統合規核查庫"
nlm source add <notebook_id> --url "https://..."
nlm source add <notebook_id> --text "..." --title "..."
nlm notebook query <notebook_id> "請列出加班費計算的法條依據"
nlm audio create <notebook_id> --confirm
nlm studio status <notebook_id>
```

若需要直接從 Codex 更新 Obsidian，優先使用：

```bash
obsidian create name="特休計算" content="# 特休計算合規核查" silent
obsidian property:set file="特休計算" name="risk_level" value="high"
obsidian append file="特休計算" content="\n## 控制點\n- CTRL-LEAVE-001"
obsidian search query="勞基法第38條" limit=20
```

## 一鍵啟動提示詞

當使用者要求「直接開始」，使用以下啟動語句：

```text
以 Feature Check 模式執行 notebooklm-global。
功能：加班費模組。
先查 NotebookLM「HR系統合規核查庫」取得法條、內規、風險與例外，
再輸出 Obsidian 合規核查文件與 PR 複查報告，
並同步更新內控對照 Base 視圖欄位（feature/law_refs/policy_refs/risk_level/status）。
```

## 代理執行規範

1. 預設輸出簡潔，僅在需求明確時輸出 JSON。
2. 建立與啟動任務後，必須記錄 notebook_id、source_id、task_id、artifact_id。
3. 長任務（research、studio）需主動回報進度。
4. 涉及刪除時，必須先顯示刪除標的並取得確認。
5. 合規結論需附「文件依據 + 條號 + 版本日期」。
