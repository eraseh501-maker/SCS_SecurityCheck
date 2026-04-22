---
name: hr-compliance-global
description: Global HR compliance skill powered by Readwise. Convert labor laws and internal policies into structured requirements and auto-generate internal control points with traceability.
---

# HR Compliance Global (Readwise)

Use this skill to build a repeatable HR compliance pipeline in any project:

1. Ingest legal and internal policy documents from Readwise Reader.
2. Convert unstructured text into a normalized requirement model.
3. Auto-generate internal control points.
4. Build requirement-to-control traceability and a gap list.

This skill is designed for ongoing use across projects and jurisdictions.

## Readwise Access

Check whether Readwise MCP tools are available (for example: `mcp__readwise__reader_search_documents`).

- If MCP is available, use MCP tools.
- If MCP is not available, use equivalent `readwise` CLI commands.

Use the same workflow either way.

## Supported Inputs

Use these source types:

- Labor regulations and administrative guidance
- Internal policy manuals and SOPs
- Employee handbook sections
- Audit findings and corrective actions
- Contract templates and HR operation playbooks

Recommended Reader tags:

- `hr-law`
- `hr-policy`
- `hr-sop`
- `hr-audit`
- `hr-payroll`
- `hr-attendance`
- `hr-leave`
- `hr-termination`
- `hr-privacy`

## Output Artifacts

Always create or update these files in the current project.

- `compliance/requirements/requirements.ndjson`
- `compliance/controls/control_points.ndjson`
- `compliance/traceability/requirement_control_map.ndjson`
- `compliance/reports/gap_report.md`
- `compliance/reports/control_summary.md`
- `compliance/glossary/domain_terms.md`

## Obsidian Handoff (Global)

After generating requirements and controls, also prepare Obsidian-ready outputs so teams can review in a knowledge workflow:

- `Obsidian/飛騰雲端HR系統/合規核查/*.md`
- `Obsidian/飛騰雲端HR系統/合規報告/*.md`
- `Obsidian/飛騰雲端HR系統/內控對照表/功能_法條_內控辦法對照.base`
- `Obsidian/飛騰雲端HR系統/內控對照表/功能法遵追溯.canvas`

When this handoff is requested, apply these conventions:

1. Markdown notes follow Obsidian frontmatter with fields: `feature`, `law_refs`, `policy_refs`, `risk_level`, `status`, `owner`, `review_date`.
2. Base view must expose at least `feature`, `law_refs`, `policy_refs`, `risk_level`, `status`.
3. Canvas must include feature nodes, legal reference nodes, policy nodes, and control nodes with labeled edges.
4. Keep requirement/control IDs unchanged between NDJSON and Obsidian notes for traceability.

## Taiwan Preset (Recommended)

This skill ships with a Taiwan-ready baseline profile and seed controls:

- `tw_profile.json`: topic taxonomy, risk weights, and required outputs for Taiwan HR compliance.
- `tw_control_seed.ndjson`: starter control library for attendance, payroll, leave, termination, benefits, harassment process, and privacy.
- `runbook_zh-TW.md`: Chinese quick start and operating guidance.

When the user requests Taiwan compliance, always load `tw_profile.json` first and initialize controls from `tw_control_seed.ndjson` before generating net-new controls.

## Canonical Requirement Model

Each requirement record must follow this normalized structure:

```json
{
  "requirement_id": "REQ-LAW-00123",
  "source_type": "external_law",
  "source_title": "Labor Standards Act",
  "source_document_id": "reader_doc_id",
  "jurisdiction": "TW",
  "article_ref": "Article 24",
  "clause_ref": "Paragraph 2",
  "effective_date": "2025-01-01",
  "topic": "overtime_pay",
  "requirement_text": "Overtime wages must be calculated...",
  "normalized_obligation": "System must compute overtime premium using legal multipliers.",
  "applies_to": ["full_time_employee"],
  "trigger_event": "overtime_approved",
  "deadline_rule": "payroll_cycle_close",
  "required_evidence": ["timesheet", "approval_log", "payroll_slip"],
  "risk_level": "high",
  "penalty_or_impact": "Regulatory fine and wage dispute exposure",
  "owner_function": "HR_Payroll",
  "review_cycle": "quarterly",
  "tags": ["hr-law", "payroll", "overtime"],
  "confidence": 0.86,
  "status": "active"
}
```

## Canonical Control Point Model

Each control point must map to one or more requirements.

```json
{
  "control_id": "CTRL-PAY-00045",
  "control_name": "Overtime premium validation before payroll close",
  "control_type": "preventive",
  "automation_level": "semi_automated",
  "objective": "Prevent underpayment of overtime wages",
  "frequency": "per_payroll_cycle",
  "process_area": "payroll",
  "owner_role": "Payroll_Manager",
  "input_data": ["timesheet", "approval_log", "employee_master"],
  "rule_logic": "If overtime_hours > 0 then apply legal multiplier by day type",
  "thresholds": {
    "variance_pct": 0.0
  },
  "exceptions_handling": "Create exception ticket and block payroll release",
  "evidence_artifacts": ["validation_report", "exception_ticket", "payroll_register"],
  "linked_requirements": ["REQ-LAW-00123"],
  "residual_risk": "medium",
  "test_procedure": "Sample 25 records and recalculate overtime premium",
  "kri": ["overtime_payment_error_rate"],
  "status": "proposed"
}
```

## Operating Modes

Support two run modes.

### Mode A: Bootstrap (First Project Run)

Use this for first-time setup.

1. Pull source corpus from Reader.
2. Build glossary and topic taxonomy.
3. Extract requirements into `requirements.ndjson`.
4. Generate baseline controls into `control_points.ndjson`.
5. Build traceability map.
6. Produce initial reports.

### Mode B: Delta Update (Recurring Run)

Use this for weekly or monthly updates.

1. Pull only documents updated since last run.
2. Detect changed requirements by source article reference and semantic diff.
3. Mark obsolete controls as `needs_review`.
4. Generate new controls only for new or materially changed requirements.
5. Refresh reports with change summary.

## End-to-End Workflow

Follow this exact sequence.

### Step 1: Discover Relevant Documents

Use Reader search/list with focused queries and tags.

- Query examples: `勞基法`, `工時`, `加班`, `請假`, `資遣`, `薪資`, `個資`, `性騷擾`, `職災`.
- Filter by relevant tags and date ranges.
- Keep response fields lean at first: title, tags, summary, saved_at, published_date, url.

### Step 2: Read Source Content

For each selected document:

1. Fetch full content (`reader_get_document_details`).
2. Preserve source metadata.
3. Split long text into article- or section-level chunks.

### Step 3: Extract Structured Requirements

For each chunk, produce requirement candidates with strict fields.

Extraction rules:

- One legal obligation per requirement whenever possible.
- Keep `requirement_text` as close to source language as possible.
- Write `normalized_obligation` as system-action language.
- Assign `risk_level` based on legal, financial, and employee-impact severity.
- If uncertain, keep record and lower confidence instead of dropping.

### Step 4: Normalize and Deduplicate

- Deduplicate by semantic similarity plus source reference.
- Merge near-duplicates and keep strongest citation.
- Enforce ID format:
  - Requirement: `REQ-{LAW|POL}-{5 digits}`
  - Control: `CTRL-{AREA}-{5 digits}`

### Step 5: Auto-Generate Internal Control Points

Generate controls from each requirement using this mapping logic:

- Start from matching seed controls in `tw_control_seed.ndjson` if Taiwan preset is active.
- Clone and adapt seed controls when the requirement intent matches but thresholds differ.
- Create new controls only when no suitable seed exists.

- If requirement is transaction-time critical, add preventive control.
- If requirement has delayed detectability, add detective control.
- If legal exposure is high, add compensating escalation control.

Control generation template:

1. Control objective from normalized obligation.
2. Trigger/frequency from deadline rule and process cadence.
3. Rule logic from legal thresholds and conditions.
4. Evidence artifacts from required evidence list.
5. Testing procedure from risk level:
   - high: monthly sample and recalculation
   - medium: quarterly sample
   - low: semiannual review

### Step 6: Build Traceability Matrix

For every requirement, ensure at least one linked control.

Traceability record shape:

```json
{
  "map_id": "MAP-000001",
  "requirement_id": "REQ-LAW-00123",
  "control_id": "CTRL-PAY-00045",
  "coverage_type": "direct",
  "coverage_strength": "strong",
  "notes": "Covers overtime multiplier computation and approval gating"
}
```

### Step 7: Gap Detection

Detect and report these gap classes:

- `missing_control`: requirement has no control
- `weak_evidence`: control has no auditable artifacts
- `owner_unassigned`: control owner missing
- `low_confidence_requirement`: extracted requirement confidence < 0.70
- `stale_policy_reference`: internal policy outdated vs law source

### Step 8: Report Generation

Always produce two reports:

1. `gap_report.md`
2. `control_summary.md`

`gap_report.md` must include:

- Executive summary
- Top high-risk gaps
- Requirement and control IDs affected
- Recommended remediation with owner and target date

`control_summary.md` must include:

- Controls by process area
- Preventive/detective mix
- Automation coverage
- High-risk requirement coverage ratio

If Taiwan preset is active, add a section: `Taiwan Critical Areas` and summarize coverage for:

- overtime and wage calculation
- leave entitlement and expiry
- termination and severance timeline
- labor and health insurance timeliness
- sexual harassment case SLA
- HR personal data access governance

## Quality Gates

Do not complete a run unless all gates pass.

1. Schema compliance: 100% of records valid JSON shape.
2. Traceability completeness: every active requirement linked to at least one control.
3. Evidence completeness: every control has evidence artifacts.
4. Confidence review: all low-confidence requirements listed in gap report.
5. No silent drops: unresolved extraction ambiguity must be logged.

## Interaction Style

When running this skill, communicate in concise phases:

1. Corpus discovery complete
2. Requirement extraction complete
3. Control generation complete
4. Gap analysis complete
5. Reports written

At each phase, report counts:

- documents processed
- requirements extracted
- controls generated
- uncovered requirements

## Safety and Governance

- This skill supports compliance engineering, not legal advice.
- Flag jurisdiction ambiguity explicitly.
- Preserve citations so legal or HR reviewers can validate outputs.
- Never delete old controls on delta run; mark as `needs_review` first.

## Optional Enhancements

If requested, also produce:

- `compliance/testpacks/control_test_cases.ndjson`
- `compliance/metrics/compliance_kri_dashboard.md`
- `compliance/change-log/regulatory_deltas.md`

## Quick Start Prompt

Use this exact kickoff instruction in any future project:

"Run hr-compliance-global in Bootstrap mode using Readwise sources tagged hr-law, hr-policy, hr-sop. Build structured requirements, generate control points, create traceability, and write gap and control summary reports under the compliance/ directory."

## Taiwan Quick Start Prompt

Use this kickoff for Taiwan HR projects:

"Run hr-compliance-global in Bootstrap mode using Readwise sources tagged hr-law, hr-policy, hr-sop, and apply Taiwan profile from tw_profile.json. Initialize from tw_control_seed.ndjson, extract requirements, generate controls, build traceability, and write all compliance outputs including gap and control summary reports."