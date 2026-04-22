# hr-compliance-global 快速操作手冊（繁中）

此手冊提供在任何專案中啟用 hr-compliance-global 的標準流程。

## 1) 先備條件

- 可使用 Readwise MCP，或已安裝並登入 readwise CLI。
- Reader 文件已完成初步標籤，建議至少包含：
  - hr-law
  - hr-policy
  - hr-sop

## 2) 首次導入（Bootstrap）

建議直接使用下列啟動語句：

```text
Run hr-compliance-global in Bootstrap mode using Readwise sources tagged hr-law, hr-policy, hr-sop, and apply Taiwan profile from tw_profile.json. Extract requirements, generate controls from tw_control_seed.ndjson, build traceability, and write all compliance outputs.
```

預期輸出：

- compliance/requirements/requirements.ndjson
- compliance/controls/control_points.ndjson
- compliance/traceability/requirement_control_map.ndjson
- compliance/reports/gap_report.md
- compliance/reports/control_summary.md

## 3) 例行更新（Delta）

每週或每月建議執行：

```text
Run hr-compliance-global in Delta Update mode with Taiwan profile. Pull only Readwise documents updated since last run, refresh changed requirements and impacted controls, and update reports.
```

## 4) 內控點調校建議

- 先確認高風險主題覆蓋率：加班費、資遣、勞健保、性騷申訴、個資。
- 每個 control 至少要有 2 份 evidence artifacts。
- 對 low confidence requirement（< 0.70）安排人工法務覆核。

## 5) 建議角色分工

- HR Ops：資料維護與流程修正
- Payroll：薪酬與稅務規則控制
- Legal/Compliance：法規解讀與覆核
- IT/IS：權限、稽核軌跡、整合與自動化

## 6) 注意事項

- 此技能為合規工程輔助，不構成法律意見。
- 實際法規適用與最終判定應由法務或外部顧問審核。
