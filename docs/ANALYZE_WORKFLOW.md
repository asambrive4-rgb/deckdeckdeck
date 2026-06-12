# Codex Analysis-First Workflow

Use this workflow when the user asks to inspect, improve, refactor, or fix a bounded code area: feature, module, file, screen, data flow, sync flow, API flow, state flow, or bug area.

Codex must not edit code immediately in this workflow. First inspect the related code, report findings, propose a safe plan, and wait for explicit user approval.

## Before approval

Only read and inspect code.

Do not edit, create, delete, rename, format, refactor, add tests, change logic, add/remove dependencies, or run cleanup that modifies files.

Analyze only the user-named area and directly related surrounding code needed to understand impact.

## Analysis scope

Identify:

- entry points and directly related files
    
- key functions, classes, components, hooks, services, or modules
    
- data, state, event, API, persistence, error, or sync flows
    
- duplicated, scattered, inconsistent, or unnecessarily complex logic
    
- nearby code likely to be affected
    

Base findings on actual code. Separate confirmed issues from assumptions. Do not propose unrelated large refactors.

## First response

Provide one concise report covering:

- current structure and relevant code flow
    
- key issues, root causes, and impact
    
- code evidence
    
- confirmed issues vs assumptions
    
- priority and modification risks
    
- grouped solution directions
    
- step-by-step implementation plan
    
- request for approval before implementation
    

Group related issues by improvement direction, not only by file or isolated fix. Explain why each group belongs together, expected benefit, rough scope/risk, and verification method.

End with:

“위 분석과 실행 계획을 기준으로 구현을 진행해도 될까요? 승인해 주시면 계획한 순서대로 코드 변경을 시작하겠습니다.”

## Implementation plan

Plan small, safe, verifiable steps.

Each step should state:

- purpose
    
- files or areas to change
    
- work to perform
    
- issues addressed
    
- risk
    
- verification method
    
- completion criteria
    

Rules:

- avoid changing many files at once
    
- preserve behavior where possible
    
- separate structural changes from behavior changes
    
- put risky changes later
    
- keep each step independently verifiable
    

## After approval

Implement only the approved plan, in order.

After each step, summarize changed files, main changes, issues resolved, behavior checked, test/build/lint results if available, remaining risks, and next step.

Avoid unrelated style, formatting, naming, or cleanup changes. If behavior changes, explain why. Mark uncertain points as assumptions.

## Final report

After all approved steps are complete, summarize:

- initial key issues
    
- issues solved
    
- changed files and core structure
    
- preserved behavior
    
- improvements made
    
- remaining risks or follow-up work
    
- verification results