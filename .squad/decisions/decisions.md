# Decisions

## SQL Comments Should Be Ignored From Schema Validation
**Timestamp:** 2026-03-06T13:44Z  
**Source:** Andrew  
**Status:** Implemented

SQL comments (line comments `--` and block comments `/* */`) should not interfere with schema validation or write-query detection. Solution: Created `SqlHelper.StripSqlComments()` utility to safely remove both comment types while preserving string literals. Applied at validation/detection points throughout the codebase.

---

## Shared SQL Comment Stripping via SqlHelper
**Date:** 2025-07-24  
**Author:** Samwise (Backend Dev)  
**Status:** Implemented

SQL comments (`--` and `/* */`) were causing false positives in schema validation and preventing correct detection of write queries. Created a shared `SqlHelper.StripSqlComments(string sql)` static method in the Llm project. Uses compiled `GeneratedRegex` to strip both comment styles while preserving string literals. Applied at validation/detection points only — original SQL is preserved for display and audit logging.

**Consequences:**
- All SQL analysis (schema validation, write-query detection) operates on comment-free SQL
- String literals containing `--` or `/*` are not incorrectly stripped
- Future SQL analysis should use `SqlHelper.StripSqlComments()` before regex matching
