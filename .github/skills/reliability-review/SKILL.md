---
name: reliability-review
description: Assess operational readiness and service reliability. Use for architecture reviews, production-readiness checks, observability plans, SLOs, runbooks, incident follow-up, capacity planning, or rollout and rollback design.
---

# Reliability Review

1. Identify critical user journeys, service boundaries, dependencies, state, and failure domains.
2. Establish available evidence: incidents, telemetry, load results, error budgets, architecture, and deployment history. Label assumptions where evidence is absent.
3. Evaluate:
   - availability and recovery targets;
   - timeout and retry behavior;
   - idempotency and duplicate handling;
   - rate limits, backpressure, and capacity;
   - data integrity and migration safety;
   - dependency failure and graceful degradation;
   - deployment, rollback, and feature isolation;
   - telemetry quality and alert actionability.
4. Rank risks by user impact, likelihood, detectability, and recovery difficulty.
5. Recommend controls with an owner, validation method, and rollback or escape path.
6. Define service level indicators from user-observable outcomes before proposing objectives.
7. For runbooks, include trigger, impact, diagnosis, mitigation, verification, recovery, escalation, and follow-up.
8. Never perform production mutations or externally visible incident actions without explicit approval.
