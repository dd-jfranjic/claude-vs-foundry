# Claude Code for Visual Studio — Foundry-friendly build (fork)

A fork of [nachum-shmilovitz-66/claude-code-visualstudio](https://github.com/nachum-shmilovitz-66/claude-code-visualstudio)
(MIT) — an unofficial Visual Studio extension that hosts the real
[Claude Code](https://www.anthropic.com/claude-code) CLI in a native chat tool window.

This fork focuses on making the extension behave correctly when the CLI authenticates
through **environment-key providers** — Microsoft Foundry, Amazon Bedrock, Google Vertex,
or a plain API key — instead of `claude login`:

- No false "Not signed in" banner when env credentials are present (Foundry/Bedrock/Vertex/API key).
- `--model` is omitted on third-party hosts when the picker is on Default, so the CLI/env
  deployment name rules (first-party model ids don't exist on those hosts).
- The model picker on third-party hosts offers Default (deployment) + Custom; the Custom
  palette asks for the provider's deployment name instead of suggesting first-party ids.
- Reasoning effort is sent as the CLI's `--effort` flag (xhigh default) instead of the
  legacy thinking-token budget.
- The usage screen explains env-key auth instead of showing "Not logged in".

Everything else — features, requirements, install — is the upstream extension; see the
[upstream README](https://github.com/nachum-shmilovitz-66/claude-code-visualstudio#readme).

## Build

Built by GitHub Actions on tags (`release.yml`, Windows runner): three VSIX flavors —
VS 2022/2026, VS 2019, VS 2017. Tests (`ci.yml`, MSTest) run on every push to `develop`.

Local build on Windows: MSBuild via any Visual Studio 2022/2026 install —
`msbuild -t:Restore,Build -p:Configuration=Release ClaudeCode.sln`.

## License

MIT — see [LICENSE](LICENSE) and [NOTICE](NOTICE). Not affiliated with Anthropic or Microsoft.
