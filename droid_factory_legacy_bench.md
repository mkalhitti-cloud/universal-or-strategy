# Legacy-Bench: Can AI Agents Maintain the World's Most Critical Software?
By Leo Tchourakov, Abhay Singhal, Eno Reyes - April 1, 2026

A new benchmark designed to measure frontier AI agent capabilities on legacy software engineering tasks spanning COBOL, Fortran, Java 7, and more.

## What is Legacy-Bench
Legacy-Bench consists of hundreds of tasks across six legacy language families: COBOL (46%), Java 7 (32%), BASIC (6%), C89 (5%), Fortran (5%), and Assembly (5%). Tasks cover bug fixing, implementing new functionality, and migrating code.

## Results & Key Findings
*   **Overall pass rates:** 16.9% to 42.5% across 12 model-agent combinations. (Compared to >70% on modern benchmarks).
*   **Feedback Loops:** Java 7 bug fixing scores highest because exceptions/stack traces provide clear feedback. COBOL bug fixing scores lowest because errors (like wrong PIC clauses or off-by-one packed decimals) fail silently.
*   **Bug Fixing vs Implementation:** Bug fixing scores roughly twice as high as new implementation, which scores twice as high as migration.
*   **Agent Scaffolding Matters:** Droid consistently outperforms the model providers' raw agents. Legacy tasks reward systematic verification, compile-run-verify loops, and format precision.

## Model Comparison
*   **GPT-5.3-Codex:** Leads overall (42.5%) and leads on COBOL (34.8%), but scores poorly on C.
*   **Gemini-3.1-Pro:** The most balanced profile (38.7%), with strong BASIC results.
*   **Opus 4.6:** Strong on Java 7 and C, but scores worst on COBOL (18.1%).
*   **GLM-5:** Competitive on Java 7 and Fortran migration.

## Failure Patterns
Agents reliably finish tasks and believe they succeeded, but fail due to:
1.  Subtle logic bugs cascading through dependent values.
2.  Missing feature subsets (handling common paths but missing edge cases).
3.  Output format mismatches (format is the interface).
4.  Byte-level precision errors (trailing newlines, field length).
5.  Spec misinterpretation.
