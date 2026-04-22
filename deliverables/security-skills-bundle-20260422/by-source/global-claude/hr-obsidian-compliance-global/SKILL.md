---
name: hr-obsidian-compliance-global
description: >
  HR 系統法規與內控整合全域技能（繁中）。整合 Readwise、NotebookLM、Obsidian、Claude Code，
  並吸收 obsidian-skills-main（obsidian-markdown / obsidian-bases / json-canvas /
  obsidian-cli / defuddle）能力，將法規與內規文件結構化並產生可追溯內部控制點。
---

# HR Obsidian Compliance Global（繁中）

此技能給「HR 系統開發 + 合規查驗」使用，目標是把法規、內規與程式邏輯連成可審計鏈路。

## 工具角色

| 工具 | 在流程中的角色 |
|---|---|
| Readwise | 收集外部法規來源（條文、函釋、修法公告） |
| NotebookLM | 將法規文件 + 內控辦法綁定為封閉知識庫，做合規問答 |
| Obsidian | 儲存核查結果、需求決策、SRS 草稿、追溯圖 |
| Claude Code | 串接上述工具，執行開發與合規查驗流程 |

## 內建 Obsidian 能力對應（來自 obsidian-skills-main）

| 來源技能 | 實際用途 |
|---|---|
| obsidian-markdown | 產出標準化合規筆記與報告（frontmatter + 章節模板） |
| obsidian-bases | 建立功能/法條/內規/控制點的資料視圖與篩選 |
| json-canvas | 建立法遵追溯圖（Feature -> Law -> Policy -> Control） |
| obsidian-cli | 以 CLI 批次建立、更新、搜尋文件 |
| defuddle | 法規網站內容去雜訊，轉乾淨 Markdown 後再入庫 |

## Step 1：建立法規知識庫（Readwise）

1. 建立 Tag：`台灣勞動法規`、`勞健保`、`所得稅`、`內控辦法`。
2. 匯入來源：
   - 勞動部法規查詢系統條文（Web Clipper）。
   - 函釋 PDF（轉為 Readwise document）。
   - 公司內控制度辦法、薪資辦法（標記 `內控文件`）。
3. 網頁內容先以 Defuddle 抽取正文，再匯入 Readwise，避免雜訊。

## Step 2：建立合規知識庫（NotebookLM）

建立 Notebook：`HR系統合規核查庫`。

至少匯入：

- 勞動基準法全文
- 勞工保險條例
- 全民健康保險法
- 就業保險法
- 所得稅法（薪資相關）
- 內部控制制度辦法（人事循環）
- 薪資作業辦法

建庫後：

1. 產生 Audio Overview。
2. 每次開發前先問衝突問題（法條 vs 內規）。

## Step 3：開發流程內嵌合規（Claude Code + Obsidian）

### 3a. 功能開發前核查

對 NotebookLM 查詢後，輸出到 Obsidian：

- 相關法條
- 法定計算規則
- 公司內規補充
- 潛在風險點

輸出路徑範例：`/飛騰雲端HR系統/合規核查/特休計算.md`

### 3b. 程式撰寫時附法源

每段關鍵計算邏輯附：

- 法條條號
- 內規條號
- 例外情境說明

### 3c. PR 後合規複查

輸出路徑範例：`/飛騰雲端HR系統/合規報告/加班費模組_YYYYMMDD.md`

報告至少包含：

1. 覆蓋條文清單
2. 未覆蓋例外情境
3. 控制點調整建議
4. 測試證據（測試案例/查詢結果/截圖）

## Obsidian 資料夾結構（建議）

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
    功能_法條_內控辦法對照.base
    功能法遵追溯.canvas
```

## Obsidian 文件最小 frontmatter

```yaml
---
title: 特休計算合規核查
feature: annual_leave_auto_calc
module: leave
law_refs:
  - 勞基法第38條
policy_refs:
  - 飛騰薪資辦法第X條
risk_level: high
status: draft
owner: HRIS
review_date: 2026-03-22
---
```

## 內控對照 Base 最小欄位

- `feature`
- `law_refs`
- `policy_refs`
- `risk_level`
- `status`

## Canvas 追溯圖最小節點

1. 功能
2. 法條
3. 內規
4. 控制點

邊線標記：`依據`、`衝突`、`覆蓋`。

## 一鍵啟動提示詞（可直接貼）

```text
執行 hr-obsidian-compliance-global。
功能：加班費模組。
先查 NotebookLM「HR系統合規核查庫」輸出法條、內規、風險與例外；
再產出 Obsidian 合規核查文件與 PR 複查報告；
最後更新內控對照 Base 與追溯 Canvas。
```

## 注意事項

1. 本技能支援合規工程，不構成法律意見。
2. 所有結論必須附文件依據、條號與版本日期。
3. 任何刪除或覆蓋動作需先取得明確確認。
