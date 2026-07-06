# SupportAssistant

Intelligent technical-assistance desktop application with local, private AI. SupportAssistant runs
small language and embedding models on-device (ONNX Runtime) so your data never leaves your machine,
and can safely act on your system through user-approved, audited tools.

> **Status:** early development. The inference, agent, and tooling subsystems are under active
> construction — see [`docs/plans/`](./docs/plans/) for the roadmap.

## Features

- **Local-first AI** — embedding search + language generation via ONNX Runtime, accelerated with
  DirectML (Windows), ROCm (Linux/AMD), CoreML (macOS), or CPU.
- **Retrieval-augmented answers** — a built-in knowledge base grounds responses in your own files.
- **Tool-augmented agent** — the assistant can read/write files and inspect the system, gated by a
  human-in-the-loop approval flow with automatic backups and a full audit trail.
- **Privacy by design** — no telemetry, no network calls for inference; everything runs locally.

## License

Copyright (C) 2026 SupportAssistant Contributors.

This program is free software: you can redistribute it and/or modify it under the terms of the
**GNU Affero General Public License as published by the Free Software Foundation, either version 3
of the License, or (at your option) any later version** ([AGPL-3.0-or-later](./LICENSE)).

This program is distributed in the hope that it will be useful, but **WITHOUT ANY WARRANTY**; without
even the implied warranty of MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the
[GNU Affero General Public License](https://www.gnu.org/licenses/agpl-3.0.html) for more details.

SupportAssistant links [`InferenceEngine.Core`](https://github.com/intel-agency/inference-engine-lib)
(AGPL-3.0-or-later), which is why this project is licensed under the AGPL. See [NOTICE](./NOTICE) for
third-party attributions.
