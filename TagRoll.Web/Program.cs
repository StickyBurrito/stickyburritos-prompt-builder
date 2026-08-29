using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Text;
using Microsoft.Extensions.FileProviders;
using Microsoft.Win32;

const string ProductName = "Stickyburrito's Prompt Generator";
const string AppExecutableName = "Stickyburritos-Prompt-Generator.exe";
const string UninstallRegistryPath = @"Software\Microsoft\Windows\CurrentVersion\Uninstall\StickyburritosPromptGenerator";

if (args.Any(argument => string.Equals(argument, "--uninstall", StringComparison.OrdinalIgnoreCase)))
{
    RunUninstaller(args.Any(argument => string.Equals(argument, "--quiet", StringComparison.OrdinalIgnoreCase)));
    return;
}

const string DanbooruSystem = """You are a Danbooru prompt expert for ComfyUI. Convert the visual idea into precise Danbooru tags in a JSON array. Preserve named characters with character_(copyright) syntax when known. Order subjects, identities, appearance, clothing, action, expression, setting, lighting, camera, style, quality. Use underscores inside tags and do not invent details. Negative is a comma-separated string of common generation defects. Return 3-4 different camera variants, each containing the complete prompt. Output only schema-valid JSON.""";
const string PonySystem = DanbooruSystem + " The target checkpoint is Pony Diffusion V6 XL. Every complete prompt must begin with score_9, score_8_up, score_7_up, score_6_up, score_5_up, score_4_up. Include exactly one appropriate source tag from source_pony, source_furry, source_cartoon, source_anime and exactly one rating tag from rating_safe, rating_questionable, rating_explicit. Do not use generic quality tags such as masterpiece, best_quality, high_quality, highres, absurdres, very_aesthetic, or highly_detailed. Pony V6 understands both tags and natural-language concepts, but keep the output as individual Danbooru-style tag strings.";
const string KreaSystem = """You expand ideas into prompts for Krea 2 text-to-image. Write a cohesive, detailed natural-language visual description, never Danbooru tags. Default to convincing photorealism with natural materials, realistic skin and fabric, plausible light behavior, and optical camera language unless the user explicitly selects another style. Establish medium and subject, concrete appearance and action, environment, composition and viewpoint, lighting, palette, materials and texture. The supplied STYLE LOCK is mandatory for the main prompt and every variant; never substitute a different medium. Prefer observable detail over quality buzzwords. Put rendered text in quotation marks. Keep the main prompt under 220 words. Return an empty negative string and exactly three complete variants, each under 160 words, varying composition, viewpoint, or palette while retaining the locked medium. Output only schema-valid JSON.""";
const string H3System = """
You write production-ready MiniMax H3 audiovisual prompts in the concise MiniMax template style. The supplied MODE, DURATION, scene timeline, STYLE LOCK, and explicit wording are mandatory. Treat the following as the canonical structure and level of specificity. It is a formatting example only: never copy its vaporwave subject matter, titles, events, or wording unless the user requests them.

REFERENCE TEMPLATE:
Vaporwave title sequence look: pink and blue gradient palette, VHS tracking artifacts, Greek statue motifs, chrome palm trees, RGB chromatic aberration, lo-fi retro atmosphere, mood languid and nostalgic.

Timeline:
[0s-1s] VHS static opens the frame, the title "COMFYUI" appears with RGB split and a slight horizontal jitter.
[1s-2.5s] Hard cut, a Greek plaster bust close-up, pink-purple gradient sky, a pixelated sun.
[2.5s-4s] Clean "STARRING" credits appear, "LATENT" and "CONTROLNET" each shown exactly once.
[4s-5s] Final card "DIRECTED BY COMFYUI" holds, one VHS tracking glitch settling into stability.

Hard cuts only, transitions landing with tape jumps, no push-ins, no dissolves.

Audio: lo-fi vaporwave score, slow drum machine with soft bass, VHS tape-noise sample joins at 2.5s, melody fading for the last 1s.

All text must be clearly legible, do not misspell English, no Chinese characters, do not repeat names or job titles, no soft dissolves, no subtitle bars.

END REFERENCE TEMPLATE.

Build every new prompt from the user's own brief using this same five-part pattern:

<One compact visual-direction paragraph describing style, palette, texture, motifs, atmosphere, and mood.>

Timeline:
[0s-1s] <visible action, subject state, composition, camera behavior, and any exact on-screen text.>
[1s-2.5s] <next scene.>

<One compact paragraph specifying cuts/transitions and prohibited camera behavior.>

Audio: <music, ambience, sound effects, dialogue, and precisely timed audio changes.>

<One final constraint paragraph covering exact text, continuity, exclusions, and things that must not be repeated.>

Use the user's supplied scene boundaries exactly and cover the entire duration without overlaps or gaps. Use seconds in compact bracket notation, such as [0s-1s] and [1s-2.5s]. Make actions chronological and visibly achievable. Quote all rendered text exactly, preserve capitalization and spelling, and state exactly-once requirements when relevant. Keep cuts, camera motion, sound, and exclusions explicit. Do not add headings other than Timeline: and Audio:. Do not use Danbooru tags.

Do not merely summarize the scene plan. Expand every timeline entry into concrete, on-screen action while preserving every supplied action, object, background detail, camera instruction, spoken line, sound cue, and continuity requirement. Never drop an earlier requirement to make room for a later refinement.

For I2V only, place this exact sentence before the visual-direction paragraph: For the target video, at 0.00 seconds into the target video, <Picture 1> (from [Shot 1]) is fully referenced. Never contradict Picture 1 or describe a different opening frame. For T2V, do not mention Picture 1.

For I2V, the supplied PICTURE 1 LOCAL ANALYSIS is visual ground truth. Carry its subject identity, anatomy and proportions, pose, wardrobe coverage, objects, spatial layout, lighting, palette, and camera framing into the opening frame. Start motion from that exact visible state, then animate only the changes requested by the user. Do not replace, sanitize, redesign, omit, or generically summarize prominent visible details. Image clarifications control desired motion and audio but cannot silently contradict visible first-frame evidence.

Return an empty negative string and exactly three fully formatted variants. Variants may change camera choreography but must preserve duration, scene boundaries, required text, story events, medium, and audio intent. Output only schema-valid JSON.
""";
const string H3NativeSystem = """
You write strict native MiniMax H3 audiovisual prompts. The supplied MODE, DURATION, scene plan, style, dialogue, rendered text, soundscape, music, and extra instructions are mandatory.

Return exactly these three fields in this order, separated by one blank line:
integrated_multimodal_description: [Shot 1] ...

overall_soundscape: ...

non_diegetic_music: ...

[Shot 1] never has a timestamp. Each later shot starts with [Shot N] At MM:SS.mmm, and timestamps must increase within the requested duration. Prefer roughly one cut per three seconds; use camera movement instead of a cut when no genuinely new information appears. Write camera motion naturally with type, optional amplitude, and optional speed—never as trailing tags.

Every clause must describe something literally visible or audible. Give each speaking or singing person a stable (S1), (S2) identity in order of first vocal appearance. Put spoken words verbatim inside <d>[Language] ...</d>. Preserve exact wording and punctuation. For voiceover, say "says in an off-screen voiceover" and explicitly keep the on-screen person's lips closed. Keep dialogue near 2.5 words per second and prefer one speaker per shot.

Put all physically visible text in English double quotation marks, verbatim, large, legible, and high-contrast. overall_soundscape covers only ambient, action, and non-verbal physical sounds—not dialogue or score. non_diegetic_music describes audience-only score using concrete instruments, tempo, rhythm, and dynamic changes; use N/A when none is requested.

For I2V only, begin the integrated description with this exact sentence before [Shot 1]: For the target video, at 0.00 seconds into the target video, <Picture 1> (from [Shot 1]) is fully referenced. Do not contradict Picture 1. For T2V, never mention Picture 1.

Return an empty negative string and exactly three complete variants. Preserve duration, shot boundaries, rendered text, dialogue, events, and audio intent in every variant. Output only schema-valid JSON.
""";
const string VisionSystem = """
You are a local first-frame analyst for MiniMax H3 image-to-video prompting. Describe only visible evidence and give the main body the same attention as the setting. Be literal, exhaustive, and visually precise.

body_description must independently document: overall build and proportions; torso, chest, abdomen, waist, hips, arms, hands, thighs, legs and feet when visible; relative size, shape, symmetry, orientation and foreshortening; visible breasts, nipples and areolae when exposed; skin tone, texture, folds, veins, marks, tattoos and piercings that are actually visible. Never guess hidden anatomy.

pose_and_contact must explain the body's orientation, weight distribution, limb placement, joint bends, overlap/occlusion, contact with furniture or props, gaze, and facial expression. wardrobe_coverage must identify every visible garment, material, fastening and trim, exactly which body regions it covers, and which remain exposed.

When adult nudity or sexualized anatomy is plainly visible, name it directly without euphemism, coyness, moral commentary, or omission. Do not turn breasts into clothing or reduce the dominant subject to generic wording. Never identify a real person, guess a name, or infer an age. Also describe composition, environment, lighting, visual style, and plausible forward motion. Ask up to four short questions only where desired motion, camera path, sound, dialogue, or ambiguous behavior cannot be inferred. The summary must foreground the dominant subject and body details before the room. Output only schema-valid JSON.
""";
const string StandardNegative = "worst quality, low quality, normal quality, lowres, blurry, out of focus, jpeg artifacts, bad anatomy, bad proportions, bad hands, malformed hands, extra digits, fewer digits, missing fingers, extra limbs, missing limbs, fused limbs, deformed, disfigured, duplicate, cropped, text, watermark, signature, username";
const string StillImageInterviewSystem = """
You are the still-image art director for Krea 2, Danbooru, and Pony image generation. This interview defines one frozen image, never a video. It is a deep interview with a minimum target of 100 distinct questions across multiple batches. Examine the current brief, prior answers, used IDs, and questions already shown, then return exactly five concise questions about useful still-image information that has not been covered.

Explore fine-grained visual distinctions: subject identity and count; individual face, hair, skin, anatomy and proportions; every garment and material; the single captured pose, hand and foot placement, interaction, gaze and expression; foreground, midground and background; architecture and props; time of day, weather, lighting sources and color; palette and surface texture; composition, crop, viewpoint, camera height, lens, perspective, focus and depth of field; rendered text; desired exclusions and failure prevention. Camera questions may describe only the static photographic setup or viewpoint.

Never ask about audio, sound, music, dialogue, voices, camera movement, camera paths, character movement over time, animation, timelines, time slots, duration, pacing, transitions, cuts, later scenes, shot sequences, or continuity between moments. Do not ask how anything changes during the image. A visible action may be phrased only as the exact pose or instant frozen in the frame.

A later question may revisit a broad category only when it asks for a genuinely different detail. Never paraphrase a prior question. Never repeat a used ID; create specific IDs such as subject_1_hair_texture, foreground_props, or camera_height. A blank answer means deliberately skipped, so do not ask it again. Give 3-5 compact suggestions per question and allow custom answers. Return only schema-valid JSON.
""";
const string VideoInterviewSystem = """
You are the audiovisual director for MiniMax H3 video generation. This is a deep interview with a minimum target of 100 distinct questions across multiple batches. Examine the current brief, prior answers, used IDs, and questions already shown, then return exactly five concise questions about useful information that has not been covered.

Explore identity and count; appearance, wardrobe and materials; initial pose and spatial layout; chronological character action and emotional performance; foreground, background, architecture and props; lighting, weather, palette and texture; framing, lens, focus, camera height and camera movement; scene timing, cuts, transitions and continuity; dialogue, ambience, sound effects and music; rendered text; exclusions and failure prevention.

A later question may revisit a broad category only when it asks for a genuinely different detail. Never paraphrase a prior question. Never repeat a used ID; create specific IDs such as subject_1_hair_texture or shot_2_camera_height. A blank answer means deliberately skipped, so do not ask it again. Give 3-5 compact suggestions per question and allow custom answers. Return only schema-valid JSON.
""";
const string ExamplesSystem = """You create surprising, useful visual prompt starters for a local prompt generator. Return exactly three substantially different ideas. Each must be one concise sentence of 8-20 words with a clear subject, action, and setting. Avoid generic quality buzzwords. For MiniMax H3, make each idea contain visible motion suitable for video. For Krea 2, favor art direction and concrete visual design. For Danbooru or Pony, write natural-language scene ideas that can be translated into tags. Keep all three fresh and unrelated. Output only schema-valid JSON.""";

var builder = WebApplication.CreateBuilder(args);
var packagedConfig = Path.Combine(AppContext.BaseDirectory, "App", "appsettings.json");
builder.Configuration.AddJsonFile(packagedConfig, optional: true, reloadOnChange: true);
builder.Services.AddHttpClient("ollama", client =>
{
    client.Timeout = TimeSpan.FromSeconds(builder.Configuration.GetValue("Ollama:TimeoutSeconds", 300));
});
var app = builder.Build();

var packagedUi = Path.Combine(AppContext.BaseDirectory, "Web");
var uiRoot = Directory.Exists(packagedUi)
    ? packagedUi
    : Path.GetFullPath(Path.Combine(app.Environment.ContentRootPath, ".."));
var uiFiles = new PhysicalFileProvider(uiRoot);
app.UseDefaultFiles(new DefaultFilesOptions { FileProvider = uiFiles });
app.UseStaticFiles(new StaticFileOptions { FileProvider = uiFiles });

app.MapGet("/api/config", (IConfiguration config) => Results.Json(new
{
    defaultModel = config["Ollama:DefaultModel"],
    visionModel = config["Ollama:VisionModel"]
}));

app.MapGet("/api/thinking/{requestId}", (string requestId) => Results.Json(new { thinking = ThinkingStore.Get(requestId) }));

app.MapPost("/api/examples", async (ExamplesRequest request, IHttpClientFactory clients, IConfiguration config, CancellationToken cancellationToken) =>
{
    var model = string.IsNullOrWhiteSpace(request.Model) ? config["Ollama:DefaultModel"]! : request.Model;
    var direction = request.NsfwMode ? "NSFW mode is enabled. Favor varied erotic or explicit adult-oriented concepts involving adults." : "NSFW mode is disabled.";
    var user = $"Prompt dialect: {request.Target ?? "danbooru"}\nCheckpoint profile: {request.CheckpointProfile ?? "generic"}\nContent direction: {direction}\nVariation seed: {Guid.NewGuid():N}\nCreate a new random set now. Avoid the obvious default examples.";
    try
    {
        var result = await AskOllama(clients.CreateClient("ollama"), config["Ollama:Endpoint"]!, model, Schemas.Examples, ExamplesSystem, user, 0.95, cancellationToken);
        result["model"] = model;
        return Results.Json(result);
    }
    catch (HttpRequestException ex) { return Results.Json(new { error = $"Cannot reach Ollama: {ex.Message}" }, statusCode: 503); }
    catch (TaskCanceledException) { return Results.Json(new { error = "Ollama did not respond before the timeout." }, statusCode: 504); }
    catch (Exception ex) { return Results.Json(new { error = ex.Message }, statusCode: 500); }
});

app.MapPost("/api/interview", async (InterviewRequest request, IHttpClientFactory clients, IConfiguration config) =>
{
    var model = string.IsNullOrWhiteSpace(request.Model) ? config["Ollama:DefaultModel"]! : request.Model;
    var target = (request.Target ?? "danbooru").Trim().ToLowerInvariant();
    var isVideo = target == "minimax_h3";
    var workflow = target switch
    {
        "minimax_h3" => "MiniMax H3 audiovisual video",
        "krea2" => "Krea 2 single still image",
        _ => "Danbooru or Pony single still image"
    };
    var history = request.Answers is { Count: > 0 }
        ? string.Join("\n", request.Answers.Select(x => $"- {x.Question}: {x.Answer}"))
        : "None yet.";
    var alreadyShown = request.AskedQuestions is { Count: > 0 } ? string.Join("\n", request.AskedQuestions.Select(x => $"- {x}")) : "None yet.";
    var usedIds = request.AskedQuestionIds is { Count: > 0 } ? string.Join(", ", request.AskedQuestionIds) : "None yet.";
    var direction = request.NsfwMode ? "NSFW mode is enabled. When relevant, ask directly about adult erotic styling, nudity, anatomy, intimacy, or explicit action." : "NSFW mode is disabled.";
    var explored = request.AskedQuestions?.Distinct(StringComparer.OrdinalIgnoreCase).Count() ?? 0;
    var modeRule = isVideo
        ? "VIDEO MODE: temporal action, camera movement, scene timing, and audio questions are allowed."
        : "STILL IMAGE MODE: ask only about visual information present in one frozen frame. Audio and every form of movement, animation, timeline, duration, transition, or multi-scene question are forbidden.";
    var user = $"Original idea: {request.Idea}\nTarget workflow: {workflow}\nPrompt dialect: {target}\n{modeRule}\nCheckpoint profile: {request.CheckpointProfile ?? "generic"}\nContent direction: {direction}\nINTERVIEW PROGRESS: {explored}/100 questions already explored. Continue until at least 100. Return exactly 5 new questions now.\nANSWERS PROVIDED:\n{history}\nUSED QUESTION IDS — NEVER REUSE THESE IDS:\n{usedIds}\nQUESTIONS ALREADY SHOWN — DO NOT ASK THESE AGAIN OR PARAPHRASE THEM:\n{alreadyShown}";
    try
    {
        var interviewSystem = isVideo ? VideoInterviewSystem : StillImageInterviewSystem;
        var result = await AskOllama(clients.CreateClient("ollama"), config["Ollama:Endpoint"]!, model, Schemas.Interview, interviewSystem, user, requestId: request.RequestId, enableThinking: request.ShowThinking);
        result["model"] = model;
        return Results.Json(result);
    }
    catch (HttpRequestException ex) { return Results.Json(new { error = $"Cannot reach Ollama: {ex.Message}" }, statusCode: 503); }
    catch (TaskCanceledException) { return Results.Json(new { error = "Ollama did not respond before the timeout." }, statusCode: 504); }
    catch (Exception ex) { return Results.Json(new { error = ex.Message }, statusCode: 500); }
});

app.MapPost("/api/analyze-image", async (ImageAnalysisRequest request, IHttpClientFactory clients, IConfiguration config, CancellationToken cancellationToken) =>
{
    if (string.IsNullOrWhiteSpace(request.ImageBase64)) return Results.BadRequest(new { error = "Choose an image first." });
    var allowed = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "image/png", "image/jpeg", "image/webp" };
    if (!allowed.Contains(request.MimeType ?? "")) return Results.BadRequest(new { error = "Only PNG, JPEG, and WebP images are supported." });
    if (request.ImageBase64.Length > 16_000_000) return Results.BadRequest(new { error = "The processed image is too large." });
    var model = string.IsNullOrWhiteSpace(request.VisionModel) ? config["Ollama:VisionModel"] ?? "huihui_ai/qwen3-vl-abliterated:8b-instruct-q4_K_M" : request.VisionModel;
    try
    {
        var body = new JsonObject
        {
            ["model"] = model, ["stream"] = false, ["think"] = request.ShowThinking, ["format"] = Schemas.ImageAnalysis.DeepClone(),
            ["options"] = new JsonObject { ["temperature"] = 0.15, ["num_predict"] = 1800 },
            ["messages"] = new JsonArray
            {
                new JsonObject { ["role"] = "system", ["content"] = VisionSystem },
                new JsonObject { ["role"] = "user", ["content"] = $"Analyze this as MiniMax H3 Picture 1. Describe the exact first frame and ask only useful motion/audio clarifications. Content mode: {(request.NsfwMode ? "NSFW enabled—use direct, explicit anatomical language for every visibly exposed adult body feature and exact garment coverage." : "standard—remain literal about visible content without inventing details.")}", ["images"] = new JsonArray(request.ImageBase64) }
            }
        };
        using var response = await clients.CreateClient("ollama").PostAsJsonAsync(config["Ollama:Endpoint"]!, body, cancellationToken);
        var outer = await response.Content.ReadFromJsonAsync<JsonObject>(cancellationToken) ?? throw new InvalidOperationException("Ollama returned no JSON.");
        if (!response.IsSuccessStatusCode) throw new HttpRequestException(outer["error"]?.GetValue<string>() ?? response.ReasonPhrase);
        var message = outer["message"];
        var content = message?["content"]?.GetValue<string>() ?? throw new InvalidOperationException("The vision model returned no analysis.");
        var result = JsonNode.Parse(content)?.AsObject() ?? throw new JsonException("The vision model returned invalid JSON.");
        var thinking = message?["thinking"]?.GetValue<string>();
        result["ollama_thinking"] = string.IsNullOrWhiteSpace(thinking) ? null : thinking;
        result["model"] = model;
        return Results.Json(result);
    }
    catch (HttpRequestException ex) { return Results.Json(new { error = $"Cannot reach the local vision model: {ex.Message}" }, statusCode: 503); }
    catch (TaskCanceledException) { return Results.Json(new { error = "Local image analysis timed out." }, statusCode: 504); }
    catch (Exception ex) { return Results.Json(new { error = ex.Message }, statusCode: 500); }
});

app.MapPost("/api/ollama", async (PromptRequest request, IHttpClientFactory clients, IConfiguration config) =>
{
    var target = request.Target ?? "danbooru";
    var profile = request.CheckpointProfile ?? "generic";
    var model = string.IsNullOrWhiteSpace(request.Model) ? config["Ollama:DefaultModel"]! : request.Model;
    var style = request.Style ?? (target == "krea2" ? "photo" : "none");
    var system = target == "danbooru" ? profile == "pony_v6" ? PonySystem : DanbooruSystem : target == "minimax_h3" ? request.H3Format == "native" ? H3NativeSystem : H3System : KreaSystem;
    var schema = target == "danbooru" ? Schemas.Tags : Schemas.Natural;
    var user = $"Idea: {request.Idea}\n" +
               $"STYLE LOCK: {StyleLock(style)}\n" +
               $"Framing: {request.Framing ?? "auto"}\n" +
               $"Quality: {request.Quality ?? "high"}\n" +
               $"Camera/lens: {request.Camera ?? "auto"}\n" +
               $"Location: {request.Location ?? "auto"}\n" +
               $"Angle/composition: {request.Angle ?? "auto"}\n" +
               $"Pose/action: {request.Pose ?? "auto"}\n" +
               $"Actors/people: {request.Actors ?? "auto"}\n" +
               $"Interaction: {request.Interaction ?? "auto"}\n" +
               $"Content direction: {(request.NsfwMode ? "NSFW mode enabled; favor adult erotic, nude, suggestive, or explicit visual details where they fit the request" : "standard mode")}\n" +
               "Treat every value other than auto as an explicit requirement.";
    if (target == "minimax_h3")
    {
        var duration = Math.Clamp(request.H3Duration ?? 6, 4, 15);
        var mode = string.Equals(request.H3Mode, "i2v", StringComparison.OrdinalIgnoreCase) ? "I2V" : "T2V";
        var scenes = request.H3Scenes is { Count: > 0 }
            ? string.Join("\n", request.H3Scenes.Select((scene, index) => $"- Scene {index + 1}: {scene.Start:0.###}s–{scene.End:0.###}s | Visual/action: {scene.Description} | Character movement: {scene.CharacterMovement} | Emotional performance: {scene.Emotion} | Camera: {scene.Camera} | Audio/dialogue: {scene.Audio}"))
            : $"- Scene 1: 0s–{duration}s | Use the main idea as one continuous shot.";
        var imageAnswers = request.ImageAnswers is { Count: > 0 } ? string.Join("\n", request.ImageAnswers.Select(x => $"- {x.Question}: {x.Answer}")) : "None.";
        user += $"\nH3 MODE: {mode}\nTARGET DURATION: {duration} seconds\nSCENE TIMELINE:\n{scenes}\n";
        user += $"PROMPT FORMAT: {(request.H3Format == "native" ? "strict native H3 fields" : "readable timeline template")}\n" +
                $"EXACT DIALOGUE / VOICE: {request.H3Dialogue ?? "None."}\n" +
                $"EXACT ON-SCREEN TEXT: {request.H3OnscreenText ?? "None."}\n" +
                $"PHYSICAL SOUNDSCAPE: {request.H3Soundscape ?? "Infer only if clearly requested."}\n" +
                $"AUDIENCE-ONLY MUSIC: {request.H3Music ?? "N/A"}\n" +
                $"EXTRA H3 INSTRUCTIONS: {request.H3Extra ?? "None."}\n";
        if (mode == "I2V") user += $"PICTURE 1 LOCAL ANALYSIS:\n{request.ImageAnalysis ?? "No image analysis supplied; do not pretend to see an image."}\nIMAGE CLARIFICATIONS:\n{imageAnswers}\n";
    }

    try
    {
        var result = await AskOllama(clients.CreateClient("ollama"), config["Ollama:Endpoint"]!, model, schema, system, user, requestId: request.RequestId, enableThinking: request.ShowThinking);
        if (target == "danbooru")
        {
            CleanTagResult(result, (request.Idea ?? "") + (request.NsfwMode ? " nsfw" : ""), style, profile);
            result["negative"] = profile == "pony_v6" ? "" : StandardNegative;
        }
        else if (target == "minimax_h3" && request.H3Format != "native")
        {
            LockH3TemplateTimeline(result, request);
        }
        result["format"] = target;
        result["checkpoint_profile"] = profile;
        result["model"] = model;
        return Results.Json(result);
    }
    catch (HttpRequestException ex) { return Results.Json(new { error = $"Cannot reach Ollama: {ex.Message}" }, statusCode: 503); }
    catch (TaskCanceledException) { return Results.Json(new { error = "Ollama did not respond before the timeout." }, statusCode: 504); }
    catch (Exception ex) { return Results.Json(new { error = ex.Message }, statusCode: 500); }
});

if (args.Contains("--open-browser", StringComparer.OrdinalIgnoreCase))
    app.Lifetime.ApplicationStarted.Register(() =>
    {
        try { System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo("http://localhost:8765") { UseShellExecute = true }); }
        catch { /* The server remains usable even when Windows cannot open the default browser. */ }
    });

app.Run();

static async Task<JsonObject> AskOllama(HttpClient client, string endpoint, string model, JsonObject schema, string system, string user, double temperature = 0.2, CancellationToken cancellationToken = default, string? requestId = null, bool enableThinking = false)
{
    async Task<JsonObject> Send(string systemPrompt, double temperature)
    {
        if (!string.IsNullOrWhiteSpace(requestId)) ThinkingStore.Set(requestId, "");
        var body = new JsonObject
        {
            ["model"] = model, ["stream"] = enableThinking, ["think"] = enableThinking, ["format"] = schema.DeepClone(),
            ["options"] = new JsonObject { ["temperature"] = temperature, ["num_predict"] = 4096 },
            ["messages"] = new JsonArray
            {
                new JsonObject { ["role"] = "system", ["content"] = systemPrompt },
                new JsonObject { ["role"] = "user", ["content"] = user }
            }
        };
        using var response = await client.PostAsJsonAsync(endpoint, body, cancellationToken);
        if (!response.IsSuccessStatusCode) throw new HttpRequestException(await response.Content.ReadAsStringAsync(cancellationToken));
        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        using var reader = new StreamReader(stream);
        var content = new System.Text.StringBuilder();
        var thinking = new System.Text.StringBuilder();
        while (await reader.ReadLineAsync(cancellationToken) is { } line)
        {
            if (string.IsNullOrWhiteSpace(line)) continue;
            var parsed = JsonNode.Parse(line);
            if (parsed is not JsonObject chunk) continue;
            if (chunk["error"] is JsonValue error) throw new HttpRequestException(error.GetValue<string>());
            var message = chunk["message"];
            var thought = message?["thinking"]?.GetValue<string>();
            if (!string.IsNullOrEmpty(thought))
            {
                thinking.Append(thought);
                if (!string.IsNullOrWhiteSpace(requestId)) ThinkingStore.Set(requestId, thinking.ToString());
            }
            var token = message?["content"]?.GetValue<string>();
            if (!string.IsNullOrEmpty(token)) content.Append(token);
        }
        if (content.Length == 0) throw new InvalidOperationException("Ollama returned no message content.");
        var result = JsonNode.Parse(content.ToString())?.AsObject() ?? throw new JsonException("Ollama returned invalid prompt JSON.");
        result["ollama_thinking"] = thinking.Length == 0 ? null : thinking.ToString();
        return result;
    }
    try { return await Send(system, temperature); }
    catch (JsonException) { return await Send(system + " Your previous response was truncated. Be substantially more concise and ensure every JSON string and container is closed.", 0); }
}

static void CleanTagResult(JsonObject result, string idea, string style, string profile)
{
    var painting = new HashSet<string> { "oil_painting", "oil_painting_(medium)", "painting_(medium)", "painterly", "canvas_texture", "visible_brushstrokes", "brushstrokes", "impasto", "traditional_media" };
    result["tags"] = CleanTags(result["tags"], idea, style, profile, painting);
    if (result["variants"] is JsonArray variants)
        foreach (var variant in variants.OfType<JsonObject>())
            variant["tags"] = CleanTags(variant["tags"], idea, style, profile, painting);
}

static void LockH3TemplateTimeline(JsonObject result, PromptRequest request)
{
    var duration = Math.Clamp(request.H3Duration ?? 6, 4, 15);
    var scenes = request.H3Scenes?.Where(scene => !string.IsNullOrWhiteSpace(scene.Description) || !string.IsNullOrWhiteSpace(scene.CharacterMovement) || !string.IsNullOrWhiteSpace(scene.Emotion) || !string.IsNullOrWhiteSpace(scene.Camera) || !string.IsNullOrWhiteSpace(scene.Audio)).OrderBy(scene => scene.Start).ToList() ?? [];
    if (scenes.Count == 0) return;

    static string Seconds(double value) => value.ToString("0.###", System.Globalization.CultureInfo.InvariantCulture) + "s";
    var timeline = "Timeline:\n" + string.Join("\n", scenes.Select(scene =>
    {
        var start = Math.Clamp(scene.Start, 0, duration);
        var end = Math.Clamp(scene.End, start, duration);
        var parts = new List<string>();
        if (!string.IsNullOrWhiteSpace(scene.Description)) parts.Add(scene.Description.Trim().TrimEnd('.') + ".");
        if (!string.IsNullOrWhiteSpace(scene.CharacterMovement)) parts.Add("Character movement: " + scene.CharacterMovement.Trim().TrimEnd('.') + ".");
        if (!string.IsNullOrWhiteSpace(scene.Emotion)) parts.Add("Emotional performance: " + scene.Emotion.Trim().TrimEnd('.') + ".");
        if (!string.IsNullOrWhiteSpace(scene.Camera)) parts.Add("Camera: " + scene.Camera.Trim().TrimEnd('.') + ".");
        if (!string.IsNullOrWhiteSpace(scene.Audio)) parts.Add("Audio during this interval: " + scene.Audio.Trim().TrimEnd('.') + ".");
        return $"[{Seconds(start)}-{Seconds(end)}] {string.Join(" ", parts)}";
    }));

    string Apply(string prompt)
    {
        if (string.IsNullOrWhiteSpace(prompt)) return timeline;
        var existing = Regex.Match(prompt, @"(?ms)^Timeline:\s*.*?(?=\r?\n\s*\r?\n|\z)");
        if (existing.Success) return prompt[..existing.Index] + timeline + prompt[(existing.Index + existing.Length)..];
        var paragraphs = Regex.Split(prompt.Trim(), @"\r?\n\s*\r?\n", RegexOptions.None);
        if (paragraphs.Length == 1) return paragraphs[0] + "\n\n" + timeline;
        var insertAfter = paragraphs[0].StartsWith("For the target video,", StringComparison.Ordinal) && paragraphs.Length > 1 ? 2 : 1;
        return string.Join("\n\n", paragraphs.Take(insertAfter).Append(timeline).Concat(paragraphs.Skip(insertAfter)));
    }

    if (result["prompt"] is JsonValue promptValue && promptValue.TryGetValue<string>(out var prompt)) result["prompt"] = Apply(prompt);
    if (result["variants"] is JsonArray variants)
        foreach (var variant in variants.OfType<JsonObject>())
            if (variant["prompt"] is JsonValue variantValue && variantValue.TryGetValue<string>(out var variantPrompt)) variant["prompt"] = Apply(variantPrompt);
}

static JsonArray CleanTags(JsonNode? node, string idea, string style, string profile, HashSet<string> painting)
{
    var tags = (node as JsonArray ?? []).SelectMany(x => (x?.GetValue<string>() ?? "").Split(','))
        .Select(x => x.Trim().Replace(' ', '_')).Where(x => x.Length > 0).Distinct().ToList();
    tags.RemoveAll(painting.Contains);
    if (style == "painting")
    {
        foreach (var required in new[] { "digital_painting_(medium)", "western_cartoon_(style)", "cel_shading", "hard_shading", "bold_colors", "clean_color_fills" })
            if (!tags.Contains(required)) tags.Add(required);
    }
    if (profile != "pony_v6") return new JsonArray(tags.Select(x => (JsonNode?)JsonValue.Create(x)).ToArray());
    var prefix = new[] { "score_9", "score_8_up", "score_7_up", "score_6_up", "score_5_up", "score_4_up" };
    var blocked = new HashSet<string> { "masterpiece", "best_quality", "high_quality", "highres", "absurdres", "very_aesthetic", "highly_detailed", "quality" };
    tags.RemoveAll(x => blocked.Contains(x) || x.StartsWith("source_") || x.StartsWith("rating_") || prefix.Contains(x));
    var lower = idea.ToLowerInvariant();
    var source = new[] { "furry", "anthro", "feral" }.Any(lower.Contains) ? "source_furry" : lower.Contains("pony") ? "source_pony" : lower.Contains("cartoon") ? "source_cartoon" : "source_anime";
    var rating = new[] { "explicit", "sex", "nude", "naked", "nsfw" }.Any(lower.Contains) ? "rating_explicit" : new[] { "suggestive", "lingerie", "cleavage", "boudoir" }.Any(lower.Contains) ? "rating_questionable" : "rating_safe";
    return new JsonArray(prefix.Concat([source, rating]).Concat(tags).Select(x => (JsonNode?)JsonValue.Create(x)).ToArray());
}

static string StyleLock(string value) => value switch
{
    "anime" => "2D anime cel illustration with clean line art, flat color shapes, and crisp cel shading",
    "photo" => "photorealistic camera image with natural materials, realistic light behavior, and optical depth",
    "manga" => "2D black-and-white manga with inked line art, hatching, and screentones",
    "painting" => "clean Western cartoon-style digital artwork with hard-edged cel shading, bold harsh colors, crisp graphic shapes, smooth flat color fills, and polished vector-like surfaces; non-anime; absolutely no visible brushstrokes, painterly texture, canvas grain, watercolor texture, impasto, or traditional-media marks",
    _ => "no fixed medium; follow only the medium explicitly stated in the idea"
};

static void RunUninstaller(bool quiet)
{
    var installDir = Path.GetFullPath(AppContext.BaseDirectory).TrimEnd(Path.DirectorySeparatorChar);
    var executable = Path.Combine(installDir, AppExecutableName);
    var validInstall = File.Exists(executable) && Directory.Exists(Path.Combine(installDir, "App")) && Directory.Exists(Path.Combine(installDir, "Web"));
    if (!validInstall)
    {
        if (!quiet) MessageBox.Show("This copy is not inside a complete Stickyburrito's Prompt Generator installation.", "Cannot uninstall", MessageBoxButtons.OK, MessageBoxIcon.Error);
        return;
    }

    if (!quiet)
    {
        var answer = MessageBox.Show(
            "Remove Stickyburrito's Prompt Generator, its shortcuts, and its local settings?\n\nOllama and downloaded models will be kept because other local applications may use them.",
            "Uninstall Stickyburrito's Prompt Generator", MessageBoxButtons.YesNo, MessageBoxIcon.Warning, MessageBoxDefaultButton.Button2);
        if (answer != DialogResult.Yes) return;
    }

    var expectedExe = Path.GetFullPath(executable);
    foreach (var process in Process.GetProcessesByName(Path.GetFileNameWithoutExtension(AppExecutableName)))
    {
        using (process)
        {
            if (process.Id == Environment.ProcessId) continue;
            try
            {
                if (string.Equals(Path.GetFullPath(process.MainModule?.FileName ?? string.Empty), expectedExe, StringComparison.OrdinalIgnoreCase))
                {
                    process.Kill(entireProcessTree: true);
                    process.WaitForExit(5000);
                }
            }
            catch { }
        }
    }

    var shortcuts = new[]
    {
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.StartMenu), "Programs", ProductName + ".lnk"),
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory), ProductName + ".lnk")
    };
    foreach (var shortcut in shortcuts) try { if (File.Exists(shortcut)) File.Delete(shortcut); } catch { }
    try { Registry.CurrentUser.DeleteSubKeyTree(UninstallRegistryPath, throwOnMissingSubKey: false); } catch { }

    if (!quiet) MessageBox.Show("Stickyburrito's Prompt Generator will now close and remove its installed files. Ollama and downloaded models will remain installed.", "Uninstalling", MessageBoxButtons.OK, MessageBoxIcon.Information);
    var encodedPath = Convert.ToBase64String(Encoding.UTF8.GetBytes(installDir));
    var cleanupScript = $"$target=[Text.Encoding]::UTF8.GetString([Convert]::FromBase64String('{encodedPath}')); Start-Sleep -Milliseconds 1500; Remove-Item -LiteralPath $target -Recurse -Force";
    var encodedScript = Convert.ToBase64String(Encoding.Unicode.GetBytes(cleanupScript));
    var windowsPowerShell = Path.Combine(Environment.SystemDirectory, "WindowsPowerShell", "v1.0", "powershell.exe");
    var cleanup = new ProcessStartInfo(windowsPowerShell) { UseShellExecute = false, CreateNoWindow = true };
    cleanup.ArgumentList.Add("-NoProfile");
    cleanup.ArgumentList.Add("-NonInteractive");
    cleanup.ArgumentList.Add("-WindowStyle");
    cleanup.ArgumentList.Add("Hidden");
    cleanup.ArgumentList.Add("-EncodedCommand");
    cleanup.ArgumentList.Add(encodedScript);
    Process.Start(cleanup);
}

record PromptRequest(string? Idea, string? Model, string? Target, string? CheckpointProfile, string? Style, string? Framing, string? Quality, string? Camera, string? Location, string? Angle, string? Pose, string? Actors, string? Interaction, bool NsfwMode, string? H3Mode, string? H3Format, double? H3Duration, List<H3Scene>? H3Scenes, string? H3Dialogue, string? H3OnscreenText, string? H3Soundscape, string? H3Music, string? H3Extra, string? ImageAnalysis, List<InterviewAnswer>? ImageAnswers, string? RequestId = null, bool ShowThinking = false);
record InterviewRequest(string? Idea, string? Model, string? Target, string? CheckpointProfile, List<InterviewAnswer>? Answers, List<string>? AskedQuestions, List<string>? AskedQuestionIds, bool NsfwMode, string? RequestId = null, bool ShowThinking = false);
record InterviewAnswer(string Question, string Answer);
record ExamplesRequest(string? Model, string? Target, string? CheckpointProfile, bool NsfwMode);
record H3Scene(double Start, double End, string? Description, string? Camera, string? Audio, string? CharacterMovement, string? Emotion);
record ImageAnalysisRequest(string? ImageBase64, string? MimeType, string? VisionModel, bool NsfwMode, bool ShowThinking = false);

static class ThinkingStore
{
    private static readonly ConcurrentDictionary<string, string> Values = new();
    public static void Set(string id, string value) => Values[id] = value;
    public static string Get(string id) => Values.TryGetValue(id, out var value) ? value : "";
}

static class Schemas
{
    public static readonly JsonObject Tags = JsonNode.Parse("""{"type":"object","properties":{"tags":{"type":"array","items":{"type":"string"}},"negative":{"type":"string"},"variants":{"type":"array","items":{"type":"object","properties":{"name":{"type":"string"},"tags":{"type":"array","items":{"type":"string"}}},"required":["name","tags"]}}},"required":["tags","negative","variants"]}""")!.AsObject();
    public static readonly JsonObject Natural = JsonNode.Parse("""{"type":"object","properties":{"prompt":{"type":"string"},"negative":{"type":"string"},"variants":{"type":"array","items":{"type":"object","properties":{"name":{"type":"string"},"prompt":{"type":"string"}},"required":["name","prompt"]}}},"required":["prompt","negative","variants"]}""")!.AsObject();
    public static readonly JsonObject Interview = JsonNode.Parse("""{"type":"object","properties":{"questions":{"type":"array","minItems":5,"maxItems":5,"items":{"type":"object","properties":{"id":{"type":"string"},"label":{"type":"string"},"question":{"type":"string"},"suggestions":{"type":"array","items":{"type":"string"},"minItems":3,"maxItems":5}},"required":["id","label","question","suggestions"]}}},"required":["questions"]}""")!.AsObject();
    public static readonly JsonObject Examples = JsonNode.Parse("""{"type":"object","properties":{"examples":{"type":"array","minItems":3,"maxItems":3,"items":{"type":"string"}}},"required":["examples"]}""")!.AsObject();
    public static readonly JsonObject ImageAnalysis = JsonNode.Parse("""{"type":"object","properties":{"summary":{"type":"string"},"subjects":{"type":"string"},"body_description":{"type":"string"},"pose_and_contact":{"type":"string"},"wardrobe_coverage":{"type":"string"},"composition":{"type":"string"},"environment":{"type":"string"},"lighting":{"type":"string"},"visual_style":{"type":"string"},"motion_opportunities":{"type":"array","items":{"type":"string"}},"questions":{"type":"array","maxItems":4,"items":{"type":"object","properties":{"id":{"type":"string"},"label":{"type":"string"},"question":{"type":"string"},"suggestions":{"type":"array","items":{"type":"string"},"maxItems":4}},"required":["id","label","question","suggestions"]}}},"required":["summary","subjects","body_description","pose_and_contact","wardrobe_coverage","composition","environment","lighting","visual_style","motion_opportunities","questions"]}""")!.AsObject();
}
