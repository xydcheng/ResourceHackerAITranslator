# Resource Hacker AI Translator Bridge

This companion tool adds AI translation workflow around Resource Hacker without modifying the original EXE.

## What it does

- Reads text from the currently focused editor view through normal copy operations.
- Sends the text to a configured translation provider.
- Pastes the translated result back into the editor view.
- Preserves the original Resource Hacker executable.

## EXE

The packaged executable is:

- `dist\ResourceHackerAITranslator.exe`

Keep `translator.config.json` in the same folder as the EXE.

The EXE now opens a normal Windows UI. You can choose the provider, edit endpoint/model/key settings, save the config, test the provider, and trigger translation with buttons.
The lower panel has two tabs:

- `Translation Result`: shows the latest source text and translated text side by side.
- `Log`: shows operation status and errors.

When a Resource Hacker window is open, the tool also shows a small topmost floating toolbar near that window:

- `全译`: translate the current Resource Hacker editor view.
- `选译`: translate selected text in the current editor view.
- `回传`: paste the editable translation result back into Resource Hacker.
- `主窗`: show the main translator window.

The floating toolbar can be shown or hidden with the main-window `悬浮栏` button or the tray menu.

## Providers

Edit `dist\translator.config.json` and set top-level `provider` to one of:

- `openai`
- `deepseek`
- `doubao`
- `qwen`
- `microsoft`
- `google`
- `youdao`
- `custom-openai-compatible`

OpenAI, DeepSeek, Doubao, Qwen, and local OpenAI-compatible services use chat completions style APIs. Microsoft, Google, and Youdao use their own translation APIs.

Fill the matching key fields:

- OpenAI/DeepSeek/Doubao/Qwen/custom: `apiKey`, `endpoint`, `model`
- Microsoft: `apiKey`, `region`, `target`
- Google: `apiKey`, `target`
- Youdao: `appKey`, `appSecret`, `target`

## Run

```powershell
E:\AI自动汉化\resourcehacker-5.2.8.437\ai-translate-bridge\dist\ResourceHackerAITranslator.exe
```

Then open Resource Hacker, focus the editor pane, and use:

- Button `翻译整个编辑器`: translate the full focused editor view.
- Button `翻译选中内容`: translate only selected text.
- Button `保存配置`: save current provider settings to `translator.config.json`.
- Button `测试接口`: send a small test translation request through the selected provider.
- Button `Paste Result`: paste the editable translation result view back into the currently focused Resource Hacker editor.
- Button `悬浮栏`: show or hide the Resource Hacker floating toolbar.
- `Ctrl+Alt+T`: translate the full focused editor view.
- `Ctrl+Alt+Shift+T`: translate only the current selection.

## Notes

- The tool uses clipboard automation, so keep focus in the editor pane before pressing the hotkey.
- It does not execute or patch the analyzed EXE.
- For Resource Hacker scripts, the translation prompt requires direct-replacement output, the exact same number of lines as the source, one output line per input line, and strict preservation of control IDs, positions, sizes, style flags, shortcut markers, placeholders, escapes, braces, quotes, indentation, blank lines, and line order.
- If the source and translation line counts differ, the log shows a warning so you can review before compiling.
- The translation result box is editable, so you can review or adjust the result before using `Paste Result`.
- Real provider calls require valid network access and valid keys. The included smoke test verifies startup and config structure, not paid/free API credentials.
