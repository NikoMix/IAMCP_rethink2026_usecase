---
name: test-engineering
description: Plan, implement, execute, and triage risk-based tests. Use when verifying acceptance criteria, adding automated tests, investigating failures, evaluating coverage, or preparing a quality report.
---

# Test Engineering

1. Map each acceptance criterion and material risk to at least one test or an explicit rationale for not testing it.
2. Inspect the repository's frameworks, naming, fixtures, helpers, and CI commands before adding tests.
3. Prioritize:
   - critical user paths;
   - changed branches and boundaries;
   - invalid and adversarial input;
   - error handling and recovery;
   - regressions related to the change.
4. Choose the lowest test level that reliably proves the behavior. Use higher-level tests for integration boundaries and user journeys.
5. Keep tests deterministic and independent. Control time, randomness, networks, and shared state.
6. Run targeted tests first. If they pass, run the smallest relevant suite, lint, or build required by repository guidance.
7. For a failure, capture the command, environment, expected result, actual result, and minimal reproduction. Do not hide flaky or failing tests.
8. Report tested scope, passed checks, failures, gaps, and residual risk.
