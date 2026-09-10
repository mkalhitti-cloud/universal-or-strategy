# Cyclomatic Complexity (CYC) Methodology

## Background

Two counting methods are used in this project: the **Lizard** static analysis
tool and **manual McCabe counting**. They can produce different results for
the same method. This note explains why and defines the project standard.

## Lizard Tool Behaviour

Lizard counts expression-body (`=>`) methods as CYC = 1 regardless of the
number of boolean operators (`||`, `&&`) present in the body.

Example:

```csharp
// Lizard reports CYC = 1
private bool IsArmingOrderState(OrderState s) =>
    s == OrderState.Working || s == OrderState.PartFilled;
```

## Manual McCabe Counting

Manual McCabe counting treats each boolean branch operator (`||`, `&&`) in an
expression as an additional decision point, adding +1 per operator.

Example (same method as above):

```
CYC = 1 (base) + 1 (|| operator) = 2
```

## Current Impact

In all current cases both methods produce values <= 8, so there is **no
JS-013 violation** under either counting scheme.

## Project Standard

- **Compliance comments** (`// CYC=N` in source) use **manual McCabe** counts.
  These are the authoritative values for JS-013 compliance review.
- **Lizard** output is used for **trend tracking only** and is not the
  authoritative gate for JS-013 compliance.
- If a discrepancy is noted during review, defer to the manual McCabe value
  in the compliance comment.
