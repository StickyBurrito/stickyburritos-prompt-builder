"""Serve TagRoll and proxy prompt requests to local Ollama."""
from http.server import ThreadingHTTPServer, SimpleHTTPRequestHandler
from pathlib import Path
from urllib.request import Request, urlopen
from urllib.error import HTTPError, URLError
import json

ROOT = Path(__file__).resolve().parent
OLLAMA = "http://127.0.0.1:11434/api/chat"
TAG_SCHEMA = {"type":"object","properties":{"tags":{"type":"array","items":{"type":"string"}},"negative":{"type":"string"},"variants":{"type":"array","items":{"type":"object","properties":{"name":{"type":"string"},"tags":{"type":"array","items":{"type":"string"}}},"required":["name","tags"]}}},"required":["tags","negative","variants"]}
NATURAL_SCHEMA = {"type":"object","properties":{"prompt":{"type":"string"},"negative":{"type":"string"},"variants":{"type":"array","items":{"type":"object","properties":{"name":{"type":"string"},"prompt":{"type":"string"}},"required":["name","prompt"]}}},"required":["prompt","negative","variants"]}
SYSTEM = """You are a Danbooru prompt expert for ComfyUI. Convert the visual idea into precise Danbooru tags in a JSON array. Preserve named characters with character_(copyright) syntax when known. Order subjects, identities, appearance, clothing, action, expression, setting, lighting, camera, style, quality. Use underscores inside tags and do not invent details. Negative is a comma-separated string of common generation defects. Return 3-4 different camera variants, each containing the complete prompt. Output only schema-valid JSON."""
PONY_SYSTEM = SYSTEM + """ The target checkpoint is Pony Diffusion V6 XL. Every complete prompt must begin with score_9, score_8_up, score_7_up, score_6_up, score_5_up, score_4_up. Include exactly one appropriate source tag from source_pony, source_furry, source_cartoon, source_anime and exactly one rating tag from rating_safe, rating_questionable, rating_explicit. Do not use generic quality tags such as masterpiece, best_quality, high_quality, highres, absurdres, very_aesthetic, or highly_detailed. Pony V6 understands both tags and natural-language concepts, but keep the output as individual Danbooru-style tag strings."""
KREA_SYSTEM = """You expand ideas into prompts for Krea 2 text-to-image. Write a cohesive, detailed natural-language visual description, never Danbooru tags. Establish medium and subject, concrete appearance and action, environment, composition and viewpoint, lighting, palette, materials and texture. The supplied STYLE LOCK is mandatory for the main prompt and every variant; never substitute a different medium. Prefer observable detail over quality buzzwords. Put rendered text in quotation marks. Keep the main prompt under 220 words. Return an empty negative string and exactly three complete variants, each under 160 words, varying composition, viewpoint, or palette while retaining the locked medium. Output only schema-valid JSON."""
H3_SYSTEM = """You write production-ready MiniMax H3 video prompts. Create a coherent temporal shot in natural language: subject and environment, initial state, chronological action and motion, camera framing and movement, lighting and atmosphere, then dialogue, sound effects or music only when requested. The supplied STYLE LOCK is mandatory for the main prompt and every variant; never substitute a different visual medium. Describe continuous visible change, not a still-image tag list. Keep identity and spatial continuity explicit. Keep the main prompt under 240 words. Return an empty negative string and exactly three complete variants, each under 180 words, with different camera choreography but the same locked medium. Output only schema-valid JSON."""
STANDARD_DANBOORU_NEGATIVE = "worst quality, low quality, normal quality, lowres, blurry, out of focus, jpeg artifacts, bad anatomy, bad proportions, bad hands, malformed hands, extra digits, fewer digits, missing fingers, extra limbs, missing limbs, fused limbs, deformed, disfigured, duplicate, cropped, text, watermark, signature, username"
ADULT_INTERACTIONS = {"kissing, consenting adults", "romantic embrace, consenting adults", "suggestive posing together, consenting adults", "intimate touching, consenting adults", "making out, consenting adults", "explicit sexual interaction, consenting adults"}

def split_tags(values):
    result = []
    for value in values or []:
        result.extend(part.strip().replace(" ", "_") for part in str(value).split(",") if part.strip())
    return list(dict.fromkeys(result))

def style_lock(value):
    return {
        "anime": "2D anime cel illustration with clean line art, flat color shapes, and crisp cel shading",
        "photo": "photorealistic camera image with natural materials, realistic light behavior, and optical depth",
        "manga": "2D black-and-white manga with inked line art, hatching, and screentones",
        "painting": "clean Western cartoon-style digital artwork with hard-edged cel shading, bold harsh colors, crisp graphic shapes, smooth flat color fills, and polished vector-like surfaces; non-anime; absolutely no visible brushstrokes, painterly texture, canvas grain, watercolor texture, impasto, or traditional-media marks",
        "none": "no fixed medium; follow only the medium explicitly stated in the idea",
    }.get(value, "no fixed medium")

def pony_tags(idea, selected_style):
    text = idea.lower()
    source = "source_furry" if any(word in text for word in ("furry", "anthro", "feral")) else "source_pony" if "pony" in text else "source_cartoon" if "cartoon" in text else "source_anime"
    rating = "rating_explicit" if any(word in text for word in ("explicit", "sex", "nude", "naked", "nsfw")) else "rating_questionable" if any(word in text for word in ("suggestive", "lingerie", "cleavage", "boudoir")) else "rating_safe"
    return source, rating

def apply_pony_profile(tags, idea, selected_style):
    prefix = ["score_9", "score_8_up", "score_7_up", "score_6_up", "score_5_up", "score_4_up"]
    blocked = {"masterpiece", "best_quality", "high_quality", "highres", "absurdres", "very_aesthetic", "highly_detailed", "quality"}
    clean = [tag for tag in tags if tag not in blocked and not tag.startswith("source_") and not tag.startswith("rating_") and tag not in prefix]
    source, rating = pony_tags(idea, selected_style)
    return prefix + [source, rating] + clean

class Handler(SimpleHTTPRequestHandler):
    def __init__(self, *args, **kwargs): super().__init__(*args, directory=str(ROOT), **kwargs)
    def do_POST(self):
        if self.path != "/api/ollama": self.send_error(404); return
        try:
            incoming = json.loads(self.rfile.read(int(self.headers.get("Content-Length", "0"))))
            model = incoming.get("model") or "richardyoung/qwen3.6-27b-abliterated:latest"
            target = incoming.get("target") or "danbooru"
            checkpoint_profile = incoming.get("checkpoint_profile") or "generic"
            actors = incoming.get("actors") or "auto"
            interaction = incoming.get("interaction") or "auto"
            if interaction in ADULT_INTERACTIONS and (actors != "two actors" or incoming.get("adult_confirmed") is not True):
                self.reply(400, {"error":"Adult interactions require Two actors and explicit confirmation that both are consenting adults aged 18+."}); return
            schema = TAG_SCHEMA if target == "danbooru" else NATURAL_SCHEMA
            system = (PONY_SYSTEM if checkpoint_profile == "pony_v6" else SYSTEM) if target == "danbooru" else (H3_SYSTEM if target == "minimax_h3" else KREA_SYSTEM)
            selected_style = incoming.get('style','none')
            user = (f"Idea: {incoming.get('idea','')}\nSTYLE LOCK: {style_lock(selected_style)}\n"
                    f"Framing: {incoming.get('framing','auto')}\nQuality: {incoming.get('quality','high')}\n"
                    f"Camera/lens: {incoming.get('camera','auto')}\nLocation: {incoming.get('location','auto')}\n"
                    f"Angle/composition: {incoming.get('angle','auto')}\nPose/action: {incoming.get('pose','auto')}\n"
                    f"Actors/people: {actors}\nInteraction: {interaction}\n"
                    f"Adult confirmation: {'both consenting adults age 18+' if incoming.get('adult_confirmed') is True else 'not provided'}\n"
                    "Treat every value other than auto as an explicit requirement.")
            result = self.ask_ollama(model, schema, system, user, 4096)
            if target == "danbooru":
                result["tags"] = split_tags(result.get("tags"))
                for variant in result.get("variants", []): variant["tags"] = split_tags(variant.get("tags"))
                blocked = {"oil_painting", "oil_painting_(medium)", "painting_(medium)", "painterly", "canvas_texture", "visible_brushstrokes", "brushstrokes", "impasto", "traditional_media"}
                result["tags"] = [tag for tag in result["tags"] if tag not in blocked]
                for variant in result.get("variants", []): variant["tags"] = [tag for tag in variant["tags"] if tag not in blocked]
                if selected_style == "painting":
                    required = ["digital_painting_(medium)", "western_cartoon_(style)", "cel_shading", "hard_shading", "bold_colors", "clean_color_fills"]
                    result["tags"] = list(dict.fromkeys(result["tags"] + required))
                    for variant in result.get("variants", []): variant["tags"] = list(dict.fromkeys(variant["tags"] + required))
                if checkpoint_profile == "pony_v6":
                    idea = incoming.get("idea", "")
                    result["tags"] = apply_pony_profile(result["tags"], idea, selected_style)
                    for variant in result.get("variants", []): variant["tags"] = apply_pony_profile(variant["tags"], idea, selected_style)
                    result["negative"] = ""
                else:
                    result["negative"] = STANDARD_DANBOORU_NEGATIVE
            result["format"] = target
            result["checkpoint_profile"] = checkpoint_profile
            result["model"] = model; self.reply(200, result)
        except HTTPError as exc: self.reply(502, {"error":f"Ollama error {exc.code}: {exc.read().decode(errors='replace')}"})
        except (URLError, TimeoutError): self.reply(503, {"error":"Cannot reach Ollama. Start Ollama and pull the selected model."})
        except Exception as exc: self.reply(500, {"error":str(exc)})
    def ask_ollama(self, model, schema, system, user, limit):
        body = json.dumps({"model":model,"stream":False,"think":False,"format":schema,"options":{"temperature":0.2,"num_predict":limit},"messages":[{"role":"system","content":system},{"role":"user","content":user}]}).encode()
        with urlopen(Request(OLLAMA, data=body, headers={"Content-Type":"application/json"}), timeout=300) as response: outer = json.loads(response.read())
        content = outer["message"]["content"]
        try:
            return json.loads(content)
        except json.JSONDecodeError:
            retry_system = system + " Your previous response was truncated. Be substantially more concise and ensure every JSON string and container is closed."
            retry_body = json.dumps({"model":model,"stream":False,"think":False,"format":schema,"options":{"temperature":0,"num_predict":4096},"messages":[{"role":"system","content":retry_system},{"role":"user","content":user}]}).encode()
            with urlopen(Request(OLLAMA, data=retry_body, headers={"Content-Type":"application/json"}), timeout=300) as response: retry_outer = json.loads(response.read())
            return json.loads(retry_outer["message"]["content"])
    def reply(self, status, data):
        body=json.dumps(data).encode(); self.send_response(status); self.send_header("Content-Type","application/json"); self.send_header("Content-Length",str(len(body))); self.end_headers(); self.wfile.write(body)

if __name__ == "__main__":
    print("TagRoll: http://localhost:8765")
    ThreadingHTTPServer(("127.0.0.1", 8765), Handler).serve_forever()
