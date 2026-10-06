# Stickyburrito's Prompt Generator v1.3.2

- Use the responsive 8B vision model for the 32 GB installer tier, avoiding the 32B model's excessive loading and memory pressure.
- Keep the image upload picker visible and version frontend assets to refresh cached pages.

# Stickyburrito's Prompt Generator v1.3.0

- Start Krea and Danbooru prompts from an uploaded image, with editable descriptions and visual style.
- Add a short change request and build a prompt directly from the reference.
- Add remarks and a second character reference to Krea image reviews.
- Save, edit, and forget named character corrections in local memory.
- Open and exit the app through its Windows tray icon.
- Improved image-analysis progress, cancellation, retry, and error messages.
- Reduced vision context allocation to limit memory use.

# Stickyburrito's Prompt Generator v1.2.0

Local Krea output review and generation-learning update for the ComfyUI prompt companion.

## Included

- Local Ollama-powered prompt generation and interviews
- Danbooru and Pony Diffusion V6 XL prompt modes
- Krea 2 photorealistic prompt mode
- MiniMax H3 T2V and I2V timeline prompting
- Local Qwen vision analysis for uploaded I2V first frames
- Local Qwen vision review for images generated with a Krea prompt
- Krea prompt-fidelity score with matched, missed, and guide-alignment details
- One-click transfer of evaluation corrections into cumulative refinement
- Optional local generation memory that carries useful Krea lessons into future prompts
- Clear-memory control; result images are analyzed transiently and never stored
- Cumulative post-generation refinement
- VRAM-aware Windows installer
- Automatic Ollama and model setup
- Determinate installation progress with an estimated time remaining
- Uninstall from either setup or Windows Installed apps
- Uninstall preserves Ollama and its downloaded model cache
- Stable 32 GB model recommendation that avoids the malformed Q8 artifact
- Selectable installation directory and cancellable setup
- Dedicated still-image interview direction for Krea 2, Danbooru, and Pony
- No audio, camera-movement, timeline, duration, or transition questions in image modes
- Video-only questions are rejected and replaced in the interface if a local model ignores the still-image instruction
- Switching between MiniMax video and image workflows clears incompatible interview state
- Krea prompts follow the official natural-language expansion structure
- Original subjects, actions, colors, content details, and spatial relationships are preserved
- Unrequested props, characters, clothing, materials, and story elements are no longer invented
- Explicit media and style locks remain mandatory throughout every Krea variant
- Requested visible text is preserved verbatim inside quotation marks
- Already-detailed Krea briefs are polished instead of unnecessarily inflated

## Installation

Download `Stickyburritos-Prompt-Generator-Setup.exe`, run it, select the available VRAM and model package, and choose an installation folder. Model downloads may require several gigabytes of disk space.

The installer is currently unsigned, so Windows SmartScreen may display an unknown-publisher warning.
