# Copilot Instructions

## Project Guidelines
- Implement all solutions in C#; do not use Node-based third-party runtime environments in this hosting environment.
- For Comix token/signature issues:
  - Prioritize fixing the signer implementation root cause instead of adding only graceful null/error handling.
  - Avoid fallback token-guess or heuristic token strategies; correct the signer logic directly.