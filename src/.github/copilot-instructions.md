# Copilot Instructions

## Project Guidelines
- Implement all solutions in C#; do not use Node-based third-party runtime environments in this hosting environment.
- For Comix token/signature issues:
  - Prioritize fixing the signer implementation root cause instead of adding only graceful null/error handling.
  - Avoid fallback token-guess or heuristic token strategies; correct the signer logic directly.
  - Support a response parsing fallback that extracts COMIX payloads from HTML-wrapped responses (e.g., <pre>…</pre>) and also accept raw Flare JSON; treat this as a parsing fallback only, not a credential or token fallback.
  - Keep COMIX chapter-list request limit at 100; 100 works in browsers and for other requests—do not change the default.