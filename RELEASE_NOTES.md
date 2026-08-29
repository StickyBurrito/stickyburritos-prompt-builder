# Stickyburrito's Prompt Generator v1.1.1

Still-image interview correction for the local ComfyUI prompt companion.

## Included

- Local Ollama-powered prompt generation and interviews
- Danbooru and Pony Diffusion V6 XL prompt modes
- Krea 2 photorealistic prompt mode
- MiniMax H3 T2V and I2V timeline prompting
- Local Qwen vision analysis for uploaded I2V first frames
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

## Installation

Download `Stickyburritos-Prompt-Generator-Setup.exe`, run it, select the available VRAM and model package, and choose an installation folder. Model downloads may require several gigabytes of disk space.

The installer is currently unsigned, so Windows SmartScreen may display an unknown-publisher warning.
