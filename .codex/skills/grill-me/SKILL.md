---
name: grill-me
description: Interview the user relentlessly about a plan, design, architecture, or decision until reaching shared understanding; ask one focused question at a time, drill into vague answers, and do not stop after one or two questions unless the user explicitly ends the grilling. Use when user wants to stress-test a plan, get grilled on their design, or mentions "grill me".
---

# Grill Me

Interview the user relentlessly about the plan until both sides share the same mental model. Do not give a quick review; expose every important assumption, dependency, tradeoff, failure mode, and unresolved branch.

## Conversation Contract

- Ask exactly one focused question at a time while the grilling session is active.
- When `request_user_input` is available, present that question through it. After the user answers, continue the operating loop in the same work turn and present the next question through a new `request_user_input` call.
- Do not end the work turn merely because one question was answered. Continue until the ending criteria are met or execution has to yield.
- When `request_user_input` is unavailable, ask one direct text question and wait for the next user turn.
- Include the recommended answer or default position before the question, with a brief reason.
- Do not conclude after one or two questions unless the user explicitly says to stop, asks for a final summary, or the plan is genuinely fully resolved.
- If the user gives a vague, contradictory, hand-wavy, or overly broad answer, ask a sharper follow-up instead of moving on.
- If the user answers with a new branch or hidden assumption, follow that branch until it is resolved before returning to the previous branch.
- If a question can be answered by exploring the codebase, inspect the codebase instead of asking the user.

## Operating Loop

Maintain an internal map of:

- confirmed facts
- unresolved assumptions
- decision branches
- dependencies between decisions
- risks and failure modes
- terms that need shared definitions

For each question:

1. Update the map from the user's latest answer.
2. Decide the most blocking unresolved item.
3. State the current working assumption in one or two sentences.
4. Give the recommended answer or default, and why.
5. Ask one direct question that forces a concrete decision, definition, or constraint.

## Question Style

- Prefer questions that cannot be answered with "it depends."
- Ask for concrete boundaries: scope, ownership, invariants, data shape, state transitions, UX behavior, failure handling, deployment path, migration plan, or acceptance criteria.
- When there are options, name the meaningful alternatives and recommend one.
- When the plan uses ambiguous words, force definitions before discussing implementation.
- When the user says "later", "simple", "automatic", "secure", "fast", "admin", "sync", or similar overloaded terms, ask what that means operationally.

## Ending Criteria

Only stop grilling when at least one of these is true:

- The user explicitly ends the session.
- The user asks for a summary, implementation plan, or code changes.
- The core decision tree is resolved enough that further questions would be low-value.

When stopping, summarize the agreed decisions, unresolved risks, and next concrete action.
