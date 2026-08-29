(() => {
  "use strict";

  const $ = (id) => document.getElementById(id);
  const interviewQuestionTarget = 100;
  const state = { tags: [], interviewAnswers: [], interviewAsked: [], interviewAskedIds: [], refinementHistory: [], autoRegenerateTimer: null, examplesController: null, imageAnalysis: null, imageObjectUrl: null };

  function showOllamaThinking(thinking, context = "request") {
    const panel = $("thinkingPanel");
    const text = $("thinkingText");
    const summary = $("thinkingSummary");
    if (!panel || !text || !summary) return;
    const value = String(thinking || "").trim();
    panel.hidden = false;
    text.textContent = value || "This model did not return a separate thinking trace for this request.";
    summary.textContent = value ? `Show reasoning from ${context}` : "No separate reasoning returned";
  }

  function beginLiveThinking(context) {
    const requestId = globalThis.crypto?.randomUUID?.() || `${Date.now()}-${Math.random()}`;
    const panel = $("thinkingPanel");
    panel.hidden = false;
    panel.open = true;
    $("thinkingSummary").textContent = `Live reasoning from ${context}`;
    $("thinkingText").textContent = "Waiting for Ollama to begin thinking…";
    let busy = false;
    const poll = async () => {
      if (busy) return;
      busy = true;
      try {
        const response = await fetch(`/api/thinking/${encodeURIComponent(requestId)}`, { cache:"no-store" });
        if (response.ok) {
          const data = await response.json();
          if (data.thinking) $("thinkingText").textContent = data.thinking;
        }
      } catch { /* The main request reports connectivity errors. */ }
      finally { busy = false; }
    };
    const timer = setInterval(poll, 300);
    return { requestId, stop: () => { clearInterval(timer); poll(); } };
  }
  const slogans = [
    "By gooners, for gooners.",
    "To goon or not to goon.",
    "If you can think it, someone else probably has too.",
    "Your imagination called. It wants more tags.",
    "Think it. Tag it. Generate it.",
    "No judgment. Just prompts.",
    "From brainrot to batch queue.",
    "Ideas in. ComfyUI out.",
    "Making pixels questionable since five minutes ago.",
    "Prompt responsibly. Or creatively.",
    "The tags know what you meant.",
    "Your GPU deserves a weird little treat.",
    "Dream boldly. Queue locally.",
    "Where intrusive thoughts become workflows.",
    "Imagine first. Explain yourself never.",
    "One more generation won't hurt.",
    "Built for the terminally imaginative.",
    "Art direction for bad influences.",
    "Serving tags with suspicious accuracy.",
    "Because vague ideas deserve sharp pixels.",
    "Turn the brainworm into a checkpoint.",
    "We speak fluent Danbooru.",
    "Locally sourced digital mischief.",
    "Your prompt, but unreasonably specific.",
    "Give the GPU what it wants.",
    "A safe space for unsafe levels of creativity.",
    "From shower thought to sampler.",
    "More detail. Fewer regrets.",
    "The prompt gets stranger from here.",
    "For research purposes, obviously.",
    "You imagine it. Qwen interrogates it.",
    "Ask not what your checkpoint can do for you.",
    "Tags before dignity.",
    "Keep calm and increase the guidance.",
    "Born to prompt. Forced to wait for sampling.",
    "One does not simply stop at one variant.",
    "ComfyUI, meet uncomfortable specificity.",
    "The muse is local and slightly unhinged.",
    "Prompt now. Explain the folder later.",
    "Maximum imagination. Minimum cloud.",
    "Your secrets are safe with localhost.",
    "Good ideas. Bad ideas. Great tags.",
    "Turning taste into tokens.",
    "Every masterpiece starts with an oddly specific sentence.",
    "Describe your vision. We won't ask why.",
    "The queue is temporary. The folder is forever.",
    "Let the latent space sort it out.",
    "More angles than your search history.",
    "A prompt builder with no indoor voice.",
    "Pixel dreams, locally rendered.",
    "Come for the tags. Stay for variant four.",
    "Your imagination has entered the workflow.",
    "Making autocomplete nervous.",
    "The shortest distance between idea and image.",
    "Unreasonably detailed by design.",
    "Roll the tags. Respect the VRAM.",
    "Creativity at 100 percent GPU utilization.",
    "No cloud. No shame. No problem.",
    "If it fits in context, it ships.",
    "Put that thought through a sampler.",
    "We turn 'you know what I mean' into tags.",
    "A little more detail never hurt the prompt.",
    "The internet didn't need to know anyway.",
    "Private prompts. Publicly questionable taste.",
    "Your local machine knows too much.",
    "Go forth and overdescribe.",
    "Prompts so specific they need a backstory.",
    "A thousand tokens walk into a checkpoint.",
    "The art director living in your localhost.",
    "Fewer blank fields. Better weirdness.",
    "Prompt engineering for recreational purposes.",
    "When one adjective simply isn't enough.",
    "Your scene deserves another camera angle.",
    "We put the 'why' in waifu.",
    "Keep it local. Make it legendary.",
    "Describe first. Touch grass later.",
    "The GPU fan is applause.",
    "Generating evidence of imagination.",
    "Every tag tells on you a little.",
    "Not all who wander are outside the latent space.",
    "Your vision, now with underscores.",
    "The prompt thickens.",
    "Choose your tags like nobody is watching.",
    "Because default settings lack ambition.",
    "Enter the idea. Embrace the iteration.",
    "Serving handcrafted prompts at machine speed.",
    "Let Qwen ask the awkwardly specific questions.",
    "One button away from another folder.",
    "Making your concepts Comfy.",
    "The vibes are weighted correctly.",
    "Local intelligence. Global levels of nonsense.",
    "Your checkpoint's favorite accomplice.",
    "An elegant tool for uncivilized ideas.",
    "The prompt generator your browser history warned you about.",
    "We add structure to creative chaos.",
    "A better prompt is always one question away.",
    "Art is subjective. Tags are comma-separated.",
    "From imagination to iteration without leaving localhost.",
    "Stay curious. Stay specific. Stay local.",
    "Stickyburrito's: rolling prompts, not judgment."
  ];

  function rollSlogan() {
    const current = $("slogan").textContent;
    const choices = slogans.filter(value => value !== current);
    $("slogan").textContent = choices[Math.floor(Math.random() * choices.length)];
  }
  rollSlogan();

  function renderExamples(examples) {
    const usable = examples.filter(value => String(value || "").trim());
    const previous = localStorage.getItem("tagroll-last-placeholder") || "";
    const placeholderChoices = usable.filter(value => value !== previous);
    const placeholder = (placeholderChoices.length ? placeholderChoices : usable)[Math.floor(Math.random() * Math.max(1, (placeholderChoices.length ? placeholderChoices : usable).length))];
    if (placeholder) {
      $("idea").placeholder = placeholder;
      localStorage.setItem("tagroll-last-placeholder", placeholder);
    }
    const container = $("examples");
    const label = document.createElement("span"); label.textContent = "Try:";
    container.replaceChildren(label, ...usable.slice(0, 3).map((idea) => {
      const button = document.createElement("button"); button.type = "button";
      button.textContent = idea.length > 34 ? `${idea.slice(0, 32).trim()}…` : idea;
      button.title = idea; button.dataset.example = idea;
      button.addEventListener("click", () => { $("idea").value = idea; $("idea").focus(); });
      return button;
    }));
  }

  async function loadExamples() {
    const fallbackPools = {
      danbooru: ["moonlit witch racing above an autumn town", "android florist arranging bioluminescent roses", "vampire barista closing a rainy midnight cafe", "knight resting beside a crystal forest waterfall", "two rivals sharing noodles beneath neon signs", "fox spirit reading inside an abandoned train", "punk drummer performing on a stormy rooftop", "mermaid mechanic repairing a submarine engine", "ghost librarian shelving books by candlelight", "space courier napping beside a panoramic window", "dragon rider landing in a crowded marketplace", "masked dancer crossing a lantern-lit bridge"],
      krea2: ["translucent glass sculpture glowing inside brutalist architecture", "retro-futurist perfume campaign with chrome and coral", "surreal desert motel submerged beneath clear water", "editorial portrait wrapped in iridescent folded paper", "miniature rainforest growing inside a vintage television", "monolithic black villa surrounded by scarlet wildflowers", "ceramic fashion collection photographed in hard sunlight", "dreamlike grocery store built entirely from colored glass", "kinetic typography installation floating over wet concrete", "luxury spacecraft lounge inspired by seventies interiors", "biomorphic furniture exhibition in a volcanic landscape", "pastel observatory perched above an endless cloud layer"],
      minimax_h3: ["a chrome motorcycle accelerates through rain as the camera circles", "silk banners unfold across a canyon while a drone descends", "a glass fox runs through mushrooms that illuminate in sequence", "an airship passes crystal towers as sunlight sweeps the deck", "a dancer spins through smoke while the camera pushes closer", "mechanical flowers bloom rapidly across an abandoned factory", "a tidal wave freezes midair as a figure walks beneath it", "paper birds escape a book and spiral around the reader", "a neon train arrives while reflections race across the platform", "a sandstorm reveals an ancient machine slowly waking up", "a chef tosses glowing ingredients as the kitchen transforms", "an astronaut drifts toward a station while Earth rotates below"]
    };
    const fallback = fallbackPools[$("target").value] || fallbackPools.danbooru;
    if (state.examplesController) state.examplesController.abort();
    state.examplesController = new AbortController();
    const controller = state.examplesController;
    const container = $("examples"); container.innerHTML = "<span>Try:</span><span>Ollama is dreaming up examples…</span>";
    try {
      const response = await fetch("/api/examples", { method:"POST", headers:{"Content-Type":"application/json"}, signal:controller.signal, body:JSON.stringify({ model:$("model").value.trim(), target:$("target").value, checkpointProfile:$("checkpointProfile").value, nsfwMode:$("nsfwMode").checked }) });
      const data = await response.json(); if (!response.ok) throw new Error(data.error || "Example request failed");
      if (controller !== state.examplesController) return;
      renderExamples(data.examples || fallback);
    } catch (error) {
      if (error.name === "AbortError") return;
      renderExamples([...fallback].sort(() => Math.random() - .5));
      const notice = document.createElement("span"); notice.className = "example-source"; notice.textContent = "local fallback — restart the Visual Studio server for Ollama ideas";
      $("examples").append(notice);
    } finally { if (controller === state.examplesController) state.examplesController = null; }
  }

  function cancelExampleLoad() {
    if (!state.examplesController) return;
    state.examplesController.abort(); state.examplesController = null;
    $("examples").innerHTML = "<span>Try:</span><span>Examples paused while your prompt generates.</span>";
  }

  function setTheme(theme) {
    document.documentElement.dataset.theme = theme;
    $("themeToggle").textContent = theme === "dark" ? "Light mode" : "Dark mode";
    localStorage.setItem("tagroll-theme", theme);
  }
  setTheme(localStorage.getItem("tagroll-theme") || (matchMedia("(prefers-color-scheme: dark)").matches ? "dark" : "light"));

  const phraseMap = [
    [/\bgwen\s+(?:and|&)\s+(?:lindsay|linsday|lindsey)\s+from\s+total\s+drama(?:\s+island)?\b/gi, "gwen_(total_drama), lindsay_(total_drama), 2girls"],
    [/\blindsay\s+(?:and|&)\s+gwen\s+from\s+total\s+drama(?:\s+island)?\b/gi, "gwen_(total_drama), lindsay_(total_drama), 2girls"],
    [/\bgwen\b/gi, "gwen_(total_drama)"], [/\b(?:lindsay|linsday|lindsey)\b/gi, "lindsay_(total_drama)"],
    [/\btotal drama(?: island)?\b/gi, "total_drama_(series)"],
    [/\b(?:roommates?|room mates?)\b/gi, "roommates"],
    [/\b(?:super )?(?:wide |over[- ]?sized )?t[- ]?shirts?\b/gi, "oversized_shirt, t-shirt"],
    [/\b(?:eating|sharing) popcorn\b/gi, "popcorn, eating"], [/\b(?:on|upon) (?:a |the )?(?:sofa|couch)\b/gi, "sofa, sitting"],
    [/\bgoth(?:ic)? personality\b/gi, "gothic, expressionless, dark_clothes"],
    [/\bbub+ly (?:and )?cheer(?:y|ie) personality\b/gi, "cheerful, smile, energetic"],
    [/\bmultiple (?:images|shots)\b/gi, "multiple_views"], [/\bmultiple camera angles?\b/gi, "multiple_views"],
    [/\blong silver hair\b/gi, "long_hair, silver_hair"], [/\bshort silver hair\b/gi, "short_hair, silver_hair"],
    [/\blong (blonde|blond) hair\b/gi, "long_hair, blonde_hair"], [/\bshort (blonde|blond) hair\b/gi, "short_hair, blonde_hair"],
    [/\blong black hair\b/gi, "long_hair, black_hair"], [/\bshort black hair\b/gi, "short_hair, black_hair"],
    [/\blong brown hair\b/gi, "long_hair, brown_hair"], [/\bshort brown hair\b/gi, "short_hair, brown_hair"],
    [/\blong (red|blue|green|purple|pink|white) hair\b/gi, (_, c) => `long_hair, ${c}_hair`],
    [/\bshort (red|blue|green|purple|pink|white) hair\b/gi, (_, c) => `short_hair, ${c}_hair`],
    [/\b(red|blue|green|purple|pink|brown|black|golden|yellow|grey|gray) eyes\b/gi, (_, c) => `${c === "gray" ? "grey" : c}_eyes`],
    [/\blooking at (the )?viewer\b/gi, "looking_at_viewer"], [/\bfull body\b/gi, "full_body"],
    [/\bcowboy shot\b/gi, "cowboy_shot"], [/\bupper body\b/gi, "upper_body"], [/\bwide shot\b/gi, "wide_shot"],
    [/\blow angle\b/gi, "from_below"], [/\bhigh angle\b/gi, "from_above"], [/\bdynamic angle\b/gi, "dutch_angle, dynamic_pose"],
    [/\bneon lights?\b/gi, "neon_lights"], [/\bcity street\b/gi, "street"], [/\brim light(?:ing)?\b/gi, "rim_lighting"],
    [/\bdramatic light(?:ing)?\b/gi, "dramatic_lighting"], [/\bcinematic light(?:ing)?\b/gi, "cinematic_lighting"],
    [/\bdepth of field\b/gi, "depth_of_field"], [/\bmoonlit\b/gi, "moonlight"], [/\bspaceship window\b/gi, "window, spacecraft_interior"],
    [/\bdigital painting\b/gi, "digital_painting_(medium)"], [/\bblack and white\b/gi, "monochrome"],
    [/\btea party\b/gi, "tea_party"], [/\bmorning light\b/gi, "morning, sunlight"], [/\bat night\b/gi, "night"]
  ];

  const wordMap = {
    woman:"1girl", girl:"1girl", female:"1girl", women:"2girls", girls:"2girls", man:"1boy", boy:"1boy", male:"1boy", couple:"1girl, 1boy",
    witch:"witch, witch_hat", astronaut:"astronaut", cat:"cat", dog:"dog", dragon:"dragon", robot:"robot", friends:"2girls",
    confident:"confident", cute:"cute", smiling:"smile", smile:"smile", laughing:"laughing", crying:"crying", surprised:"surprised",
    dress:"dress", skirt:"skirt", jacket:"jacket", uniform:"uniform", armor:"armor", swimsuit:"swimsuit", kimono:"kimono", hat:"hat", shirt:"shirt", shirts:"shirt", tshirt:"t-shirt", oversized:"oversized_clothes",
    black:"black", white:"white", red:"red", blue:"blue", green:"green", purple:"purple", pink:"pink", silver:"silver",
    standing:"standing", sitting:"sitting", running:"running", flying:"flying", walking:"walking", kneeling:"kneeling", dancing:"dancing",
    rain:"rain", rainy:"rain, wet", snow:"snow", snowy:"snow", autumn:"autumn", winter:"winter", summer:"summer", clouds:"clouds",
    forest:"forest", beach:"beach", ocean:"ocean", mountain:"mountain", mountains:"mountain", street:"street", city:"city", tokyo:"tokyo",
    cottage:"cottage, indoors", bedroom:"bedroom, indoors", room:"room, indoors", apartment:"apartment, indoors", cafe:"cafe, indoors", rooftop:"rooftop", space:"space", earth:"earth_(planet)", sofa:"sofa", couch:"sofa", popcorn:"popcorn",
    night:"night", sunset:"sunset", sunrise:"sunrise", moon:"moon", stars:"starry_sky", window:"window", tea:"tea", cozy:"cozy", goth:"gothic", gothic:"gothic", bubbly:"cheerful", cheerful:"cheerful",
    detailed:"detailed", interior:"interior", portrait:"portrait", closeup:"close-up", solo:"solo", cinematic:"cinematic", warm:"warm_lighting"
  };

  const presets = {
    style: { anime:["anime_coloring", "illustration"], photo:["photorealistic", "realistic"], manga:["manga", "monochrome", "screentones"], painting:["digital_painting_(medium)", "western_cartoon_(style)", "cel_shading", "hard_shading", "bold_colors", "clean_color_fills"], none:[] },
    framing: { portrait:["portrait", "upper_body"], cowboy:["cowboy_shot"], full:["full_body"], wide:["wide_shot"], auto:[] },
    quality: { high:["masterpiece", "best_quality", "highres"], max:["masterpiece", "best_quality", "very_aesthetic", "absurdres", "highly_detailed"], none:[] }
  };
  const negative = "worst quality, low quality, normal quality, lowres, blurry, jpeg artifacts, bad anatomy, bad hands, malformed hands, extra digits, fewer digits, missing fingers, extra limbs, deformed, disfigured, text, watermark, signature, username";

  function normalizeTag(tag) {
    return tag.trim().toLowerCase().replace(/\s+/g, "_").replace(/^_+|_+$/g, "");
  }
  function normalizeQuestion(question) {
    return String(question).toLowerCase().replace(/[^a-z0-9]+/g, " ").trim();
  }
  function questionTopics(question) {
    const text = normalizeQuestion(question);
    const groups = {
      location:["location","setting","place","where","environment","scene set"], hair:["hair","hairstyle","haircut"],
      body:["body type","build","physique","chest","breast","bust","curvy","figure"], clothing:["clothes","clothing","outfit","wearing","wardrobe","dress"],
      action:["action","doing","pose","posing","movement"], interaction:["interaction","interact","relationship","together"], expression:["expression","emotion","mood","face","feeling"],
      lighting:["lighting","light source","illumination"], time:["time of day","daytime","nighttime","morning","evening"], camera:["camera","lens","angle","shot","framing","composition","viewpoint"],
      style:["style","medium","aesthetic","visual look","photoreal","anime"], color:["color palette","colours","colors","palette"], sound:["sound","audio","music","dialogue","voice"]
    };
    return Object.entries(groups).filter(([,terms]) => terms.some(term => text.includes(term))).map(([name]) => name);
  }
  function questionsSimilar(a, b) {
    const topicsA = questionTopics(a), topicsB = questionTopics(b);
    const sharesTopic = topicsA.some(topic => topicsB.includes(topic));
    const stop = new Set(["what","which","would","should","could","you","your","the","this","that","for","with","and","about","want","prefer","like","have","does","scene","image","subject","character","details","specific"]);
    const words = value => new Set(normalizeQuestion(value).split(" ").filter(word => word.length > 2 && !stop.has(word)).map(word => word.replace(/(?:ing|ed|es|s)$/,"")));
    const left=words(a), right=words(b); if (!left.size || !right.size) return false;
    const overlap=[...left].filter(word => right.has(word)).length;
    return overlap / Math.min(left.size, right.size) >= (sharesTopic ? .45 : .7);
  }
  function unique(items) { return [...new Set(items.map(normalizeTag).filter(Boolean))]; }

  function parseIdea(text) {
    let working = text.toLowerCase();
    const found = [];
    phraseMap.forEach(([pattern, replacement]) => {
      working = working.replace(pattern, (...args) => {
        const value = typeof replacement === "function" ? replacement(...args) : replacement;
        found.push(...value.split(","));
        return " ";
      });
    });
    const tokens = working.replace(/[^a-z0-9'-]/g, " ").split(/\s+/).filter(Boolean);
    tokens.forEach((token) => { if (wordMap[token]) found.push(...wordMap[token].split(",")); });

    const colorClothing = text.toLowerCase().match(/\b(black|white|red|blue|green|purple|pink|silver|gold)\s+(dress|skirt|jacket|shirt|coat|armor|kimono|swimsuit)\b/g) || [];
    colorClothing.forEach((p) => found.push(p.replace(/\s+/g, "_")));
    return unique(found);
  }

  function collectInterviewAnswers() {
    document.querySelectorAll(".question-card").forEach((card) => {
      const answer = card.querySelector("input").value.trim();
      if (!answer) return;
      const id = card.dataset.id;
      const entry = { id, question:card.dataset.question, answer };
      const existing = state.interviewAnswers.findIndex(item => item.id === id);
      if (existing >= 0) state.interviewAnswers[existing] = entry; else state.interviewAnswers.push(entry);
    });
    $("answerCount").textContent = `${state.interviewAnswers.length} answered`;
  }

  function renderQuestions(questions) {
    const known = new Set(state.interviewAsked.map(normalizeQuestion));
    questions.forEach(item => {
      const question = item.question || item.label || "Visual detail";
      if (!known.has(normalizeQuestion(question))) { state.interviewAsked.push(question); known.add(normalizeQuestion(question)); }
      const id = String(item.id || "").trim().toLowerCase();
      if (id && !state.interviewAskedIds.includes(id)) state.interviewAskedIds.push(id);
    });
    $("questions").replaceChildren(...questions.map((item, index) => {
      const card = document.createElement("div"); card.className = "question-card";
      card.dataset.id = item.id || `question-${Date.now()}-${index}`;
      card.dataset.question = item.question || item.label || "Visual detail";
      const label = document.createElement("label");
      label.textContent = item.label || "Visual detail";
      const detail = document.createElement("small"); detail.textContent = item.question || "What should this look like?"; label.append(detail);
      const chips = document.createElement("div"); chips.className = "suggestion-chips";
      const input = document.createElement("input"); input.placeholder = "Type your answer, choose a suggestion, or leave blank";
      (item.suggestions || []).forEach((suggestion) => {
        const chip = document.createElement("button"); chip.type = "button"; chip.className = "suggestion-chip"; chip.textContent = suggestion;
        chip.addEventListener("click", () => { input.value = suggestion; chips.querySelectorAll("button").forEach(x => x.classList.toggle("selected", x === chip)); });
        chips.append(chip);
      });
      card.append(label, chips, input); return card;
    }));
  }

  async function requestQuestions(reset = false) {
    cancelExampleLoad();
    const idea = buildPromptIdea();
    if (!idea) { $("engineStatus").textContent = "Add an overall idea, describe at least one H3 scene, or upload a first frame."; $("idea").focus(); return; }
    if ($("engine").value !== "ollama") { await generatePrompt(); return; }
    if (reset) { state.interviewAnswers = []; state.interviewAsked = []; state.interviewAskedIds = []; } else collectInterviewAnswers();
    $("interview").hidden = false; $("roll").hidden = true;
    $("questions").innerHTML = '<p class="interview-intro">Ollama is looking for useful missing details…</p>';
    $("engineStatus").textContent = "Ollama is preparing the next questions locally…";
    $("askMore").disabled = true; $("generateNow").disabled = true;
    const liveThinking = $("showThinking").checked ? beginLiveThinking("the interview batch") : { requestId:null, stop:()=>{} };
    try {
      const response = await fetch("/api/interview", { method:"POST", headers:{"Content-Type":"application/json"}, body:JSON.stringify({ idea, model:$("model").value.trim(), target:$("target").value, checkpointProfile:$("checkpointProfile").value, answers:state.interviewAnswers, askedQuestions:state.interviewAsked, askedQuestionIds:state.interviewAskedIds, nsfwMode:$("nsfwMode").checked, requestId:liveThinking.requestId, showThinking:$("showThinking").checked }) });
      const data = await response.json(); if (!response.ok) throw new Error(data.error || "Interview request failed");
      if ($("showThinking").checked) showOllamaThinking(data.ollama_thinking, "the interview batch");
      const seen = new Set(state.interviewAsked.map(normalizeQuestion));
      const seenIds = new Set(state.interviewAskedIds);
      const fresh = [];
      (data.questions || []).forEach(item => {
        const question = item.question || item.label || "";
        const id = String(item.id || "").trim().toLowerCase();
        const duplicate = !question || seen.has(normalizeQuestion(question)) || state.interviewAsked.some(previous => questionsSimilar(previous, question)) || fresh.some(previous => questionsSimilar(previous.question || previous.label || "", question));
        if (!duplicate) { fresh.push(item); seen.add(normalizeQuestion(question)); if (id) seenIds.add(id); }
      });
      const totalExplored = new Set([...state.interviewAsked, ...fresh.map(item => item.question || item.label || "")].map(normalizeQuestion)).size;
      if (fresh.length) {
        renderQuestions(fresh);
        $("askMore").hidden = false;
        $("askMore").textContent = "Ask next questions";
      } else if (totalExplored >= interviewQuestionTarget) {
        $("questions").innerHTML = '<div class="interview-complete"><span>✓</span><div><strong>Interview complete</strong><p>No useful questions remain. Generate the prompt whenever you’re ready.</p></div></div>';
        $("askMore").hidden = true;
      } else {
        $("questions").innerHTML = `<div class="interview-complete interview-continue"><span>↻</span><div><strong>${totalExplored} of ${interviewQuestionTarget} questions explored</strong><p>That batch contained repeats. Ask again for a fresh set of details.</p></div></div>`;
        $("askMore").hidden = false;
        $("askMore").textContent = "Try another question batch";
      }
      const answeredShown = new Set(state.interviewAnswers.filter(answer => answer.id !== "post-generation-feedback").map(answer => answer.id)).size;
      const uniqueAsked = new Set(state.interviewAsked.map(normalizeQuestion)).size;
      $("answerCount").textContent = `${answeredShown} answered · ${uniqueAsked}/${interviewQuestionTarget} questions explored`;
      $("engineStatus").textContent = `Questions generated locally with ${data.model || $("model").value}.`;
    } catch (error) {
      $("questions").innerHTML = `<p class="interview-intro">Could not ask questions: ${String(error.message).replace(/</g,"&lt;")}. You can still generate now.</p>`;
    } finally { liveThinking.stop(); $("askMore").disabled = false; $("generateNow").disabled = false; }
  }

  const h3CameraSuggestions = ["locked-off tripod", "slow dolly in", "slow dolly out", "gentle handheld drift", "pan left", "pan right", "tilt up", "tilt down", "orbit clockwise", "orbit counterclockwise", "lateral tracking shot", "forward tracking shot", "backward tracking shot", "crane up", "crane down", "low-angle push-in", "high-angle reveal", "rack focus foreground to subject", "whip pan into a hard cut", "POV movement"];
  const h3MovementSuggestions = ["holds perfectly still except for breathing", "slowly turns toward camera", "walks toward camera", "walks away from camera", "crosses the frame left to right", "reaches deliberately for the prop", "raises their head and meets the lens", "shifts weight and changes stance", "sits down naturally", "stands up naturally", "leans closer", "steps backward cautiously", "spins once with fabric trailing", "runs as the camera tracks", "hair and clothing move in the wind", "hands perform a precise task", "exchanges a glance with the other character", "embraces the other character", "reacts with a sudden recoil", "finishes in a stable hero pose"];
  const h3EmotionSuggestions = ["calm and self-possessed", "joyful and energetic", "languid and nostalgic", "tense and apprehensive", "confident and seductive", "playful and teasing", "melancholic and withdrawn", "angry but controlled", "shocked and breathless", "curious and alert", "determined and focused", "fearful and hesitant", "relieved and softening", "romantic and intimate", "euphoric and uninhibited", "cold and detached", "mischievous and conspiratorial", "exhausted but resolute", "dreamlike and entranced", "emotion changes visibly during the shot"];
  const optionList = (values, selected = "") => `<option value="">Choose a suggestion…</option>${values.map(value => `<option${value === selected ? " selected" : ""}>${value}</option>`).join("")}`;

  function sceneTemplate(scene = {}) {
    const card = document.createElement("article"); card.className = "scene-card";
    card.innerHTML = `<div class="scene-head"><strong>Scene</strong><div><button type="button" data-action="duplicate">Duplicate</button><button type="button" data-action="remove">Remove</button></div></div>
      <div class="scene-times"><label>Start (seconds)<input class="scene-start" type="number" min="0" max="15" step="0.001" value="${scene.start ?? 0}"></label><label>End (seconds)<input class="scene-end" type="number" min="0" max="15" step="0.001" value="${scene.end ?? Number($("h3Duration").value)}"></label></div>
      <label>Visible action and change<textarea class="scene-description" rows="3" placeholder="Describe the starting pose, visible action, environmental reaction, and final state.">${scene.description || ""}</textarea></label>
      <div class="scene-detail-grid">
        <label>Camera movement<select class="scene-camera">${optionList(h3CameraSuggestions, scene.camera || "")}</select></label>
        <label>Character movement<select class="scene-character-movement">${optionList(h3MovementSuggestions, scene.characterMovement || "")}</select></label>
        <label>Emotional state / performance<select class="scene-emotion">${optionList(h3EmotionSuggestions, scene.emotion || "")}</select></label>
        <label>Sound / dialogue<input class="scene-audio" placeholder="e.g. rain ambience; she says…" value="${scene.audio || ""}"></label>
      </div>`;
    card.querySelector('[data-action="remove"]').addEventListener("click", () => { if ($("scenes").children.length > 1) { card.remove(); numberScenes(); autoRegeneratePrompt(); } });
    card.querySelector('[data-action="duplicate"]').addEventListener("click", () => { const copy = readScene(card); card.after(sceneTemplate(copy)); numberScenes(); autoRegeneratePrompt(); });
    card.querySelectorAll("input,textarea,select").forEach(control => control.addEventListener("change", autoRegeneratePrompt));
    return card;
  }

  function numberScenes() { [...$("scenes").children].forEach((card, index) => card.querySelector(".scene-head strong").textContent = `Scene ${index + 1}`); }
  function readScene(card) { return { start:Number(card.querySelector(".scene-start").value), end:Number(card.querySelector(".scene-end").value), description:card.querySelector(".scene-description").value.trim(), camera:card.querySelector(".scene-camera").value.trim(), characterMovement:card.querySelector(".scene-character-movement").value.trim(), emotion:card.querySelector(".scene-emotion").value.trim(), audio:card.querySelector(".scene-audio").value.trim() }; }
  function collectScenes() { return [...$("scenes").children].map(readScene).sort((a,b) => a.start - b.start); }
  function resetScenes() { $("scenes").replaceChildren(sceneTemplate({ start:0, end:Number($("h3Duration").value) })); numberScenes(); }

  function renderImageQuestions(questions) {
    $("imageQuestions").replaceChildren(...(questions || []).map((item, index) => {
      const card = document.createElement("div"); card.className = "image-question"; card.dataset.question = item.question || item.label; card.dataset.id = item.id || `image-${index}`;
      const label = document.createElement("label"); label.textContent = item.question || item.label;
      const input = document.createElement("input"); input.placeholder = "Optional — leave blank to skip";
      const chips = document.createElement("div"); chips.className = "suggestion-chips";
      (item.suggestions || []).forEach(value => { const chip = document.createElement("button"); chip.type="button"; chip.className="suggestion-chip"; chip.textContent=value; chip.addEventListener("click", () => { input.value=value; autoRegeneratePrompt(); }); chips.append(chip); });
      input.addEventListener("change", autoRegeneratePrompt); card.append(label, chips, input); return card;
    }));
  }

  function collectImageAnswers() { return [...document.querySelectorAll(".image-question")].map(card => ({ question:card.dataset.question, answer:card.querySelector("input").value.trim() })).filter(item => item.answer); }

  function buildPromptIdea() {
    const mainIdea = $("idea").value.trim();
    if ($("target").value !== "minimax_h3") return mainIdea;
    const details = [];
    if (mainIdea) details.push(`Overall concept: ${mainIdea}`);
    collectScenes().forEach((scene, index) => {
      const parts = [scene.description && `visual action: ${scene.description}`, scene.characterMovement && `character movement: ${scene.characterMovement}`, scene.emotion && `emotional performance: ${scene.emotion}`, scene.camera && `camera: ${scene.camera}`, scene.audio && `audio: ${scene.audio}`].filter(Boolean);
      if (parts.length) details.push(`Scene ${index + 1} (${scene.start}s-${scene.end}s): ${parts.join("; ")}`);
    });
    [["Dialogue / voice","h3Dialogue"],["On-screen text","h3OnscreenText"],["Soundscape","h3Soundscape"],["Music","h3Music"],["Extra instructions","h3Extra"]].forEach(([label,id]) => {
      const value=$(id).value.trim(); if (value) details.push(`${label}: ${value}`);
    });
    if ($("h3Mode").value === "i2v" && state.imageAnalysis) details.push(`Uploaded Picture 1 — mandatory first-frame evidence:\n${JSON.stringify(state.imageAnalysis)}`);
    return details.join("\n");
  }

  async function prepareImage(file) {
    const bitmap = await createImageBitmap(file); const maxEdge = 1536; const scale = Math.min(1, maxEdge / Math.max(bitmap.width, bitmap.height));
    const canvas = document.createElement("canvas"); canvas.width = Math.round(bitmap.width * scale); canvas.height = Math.round(bitmap.height * scale);
    canvas.getContext("2d").drawImage(bitmap, 0, 0, canvas.width, canvas.height); bitmap.close();
    const blob = await new Promise(resolve => canvas.toBlob(resolve, "image/jpeg", .88));
    return { mimeType:"image/jpeg", imageBase64:(await new Promise((resolve, reject) => { const reader=new FileReader(); reader.onload=()=>resolve(reader.result.split(",")[1]); reader.onerror=reject; reader.readAsDataURL(blob); })) };
  }

  async function analyzeReferenceImage(file) {
    if (!file) return;
    cancelExampleLoad();
    if (state.imageObjectUrl) URL.revokeObjectURL(state.imageObjectUrl);
    state.imageObjectUrl = URL.createObjectURL(file); $("imagePreview").src = state.imageObjectUrl; $("imageWorkspace").hidden = false;
    $("imageAnalysisStatus").textContent = "Scanning locally…"; $("imageAnalysisSummary").textContent = "Qwen Vision is reading the first frame."; $("imageQuestions").replaceChildren(); state.imageAnalysis = null;
    try {
      const prepared = await prepareImage(file);
      const response = await fetch("/api/analyze-image", { method:"POST", headers:{"Content-Type":"application/json"}, body:JSON.stringify({ ...prepared, visionModel:$("visionModel").value.trim(), nsfwMode:$("nsfwMode").checked, showThinking:$("showThinking").checked }) });
      const data = await response.json(); if (!response.ok) throw new Error(data.error || "Image analysis failed");
      if ($("showThinking").checked) showOllamaThinking(data.ollama_thinking, "image analysis");
      state.imageAnalysis = data; $("imageAnalysisStatus").textContent = `Scanned with ${data.model || $("visionModel").value}`;
      $("imageAnalysisSummary").replaceChildren(...[
        data.summary,
        data.body_description && `Body: ${data.body_description}`,
        data.pose_and_contact && `Pose: ${data.pose_and_contact}`,
        data.wardrobe_coverage && `Wardrobe coverage: ${data.wardrobe_coverage}`
      ].filter(Boolean).map((text,index) => { const part=document.createElement(index ? "span" : "strong"); part.textContent=text; return part; }));
      renderImageQuestions(data.questions);
      autoRegeneratePrompt();
    } catch (error) { $("imageAnalysisStatus").textContent = "Analysis failed"; $("imageAnalysisSummary").textContent = error.message; }
  }

  async function generatePrompt(refinementInstruction = "", previousPrompt = "") {
    cancelExampleLoad();
    const idea = buildPromptIdea();
    if (!idea) { $("engineStatus").textContent = "Add an overall idea, describe at least one H3 scene, or upload a first frame."; $("idea").focus(); return; }
    if (!refinementInstruction && state.refinementHistory.length) {
      refinementInstruction = state.refinementHistory.map((item, index) => `${index + 1}. ${item}`).join("\n");
      previousPrompt = $("positive").value.trim();
    }
    collectInterviewAnswers();
    let enrichedIdea = state.interviewAnswers.length ? `${idea}\nAdditional visual details:\n${state.interviewAnswers.map(x => `- ${x.question}: ${x.answer}`).join("\n")}` : idea;
    if (refinementInstruction) {
      enrichedIdea += `\n\nCUMULATIVE MANDATORY REVISION HISTORY — APPLY EVERY ITEM UNLESS A LATER ITEM EXPLICITLY CONTRADICTS AN EARLIER ONE:\n${refinementInstruction}`;
      if (previousPrompt) enrichedIdea += `\n\nLAST SUCCESSFUL GENERATED PROMPT — revise this prompt instead of rebuilding from scratch:\n${previousPrompt}`;
      enrichedIdea += "\n\nReturn a complete rewritten prompt that visibly includes every compatible established detail and every cumulative revision. Do not silently remove previous subjects, background elements, lighting, wardrobe, anatomy, actions, camera choices, or scene events.";
    }
    const rollButton = $("generateNow");
    if ($("engine").value === "ollama") {
      rollButton.disabled = true; rollButton.firstChild.textContent = "Thinking… ";
      const liveThinking = $("showThinking").checked ? beginLiveThinking("prompt generation") : { requestId:null, stop:()=>{} };
      try {
        const response = await fetch("/api/ollama", { method:"POST", headers:{"Content-Type":"application/json"}, body:JSON.stringify({ idea:enrichedIdea, model:$("model").value.trim(), target:$("target").value, checkpointProfile:$("checkpointProfile").value, style:$("style").value, framing:$("framing").value, quality:$("quality").value, camera:$("camera").value, location:$("location").value, angle:$("angle").value, pose:$("pose").value, actors:$("actors").value, interaction:$("interaction").value, nsfwMode:$("nsfwMode").checked, h3Mode:$("h3Mode").value, h3Format:$("h3Format").value, h3Duration:Number($("h3Duration").value), h3Scenes:collectScenes(), h3Dialogue:$("h3Dialogue").value.trim(), h3OnscreenText:$("h3OnscreenText").value.trim(), h3Soundscape:$("h3Soundscape").value.trim(), h3Music:$("h3Music").value.trim(), h3Extra:$("h3Extra").value.trim(), imageAnalysis:state.imageAnalysis ? JSON.stringify(state.imageAnalysis) : null, imageAnswers:collectImageAnswers(), requestId:liveThinking.requestId, showThinking:$("showThinking").checked }) });
        const data = await response.json();
        if (!response.ok) throw new Error(data.error || "Local AI request failed");
        if ($("showThinking").checked) showOllamaThinking(data.ollama_thinking, "prompt generation");
        state.outputFormat = data.format || "danbooru";
        $("resultTitle").textContent = state.outputFormat === "minimax_h3" ? "Ready for MiniMax H3" : state.outputFormat === "krea2" ? "Ready for Krea 2" : $("checkpointProfile").value === "pony_v6" ? "Ready for Pony V6" : "Ready for Danbooru";
        if (state.outputFormat === "danbooru") { state.tags = unique(data.tags || []); render(); }
        else { state.tags = []; $("positive").value = data.prompt || ""; $("tagEditor").hidden = true; }
        $("negative").value = data.negative || (state.outputFormat === "danbooru" ? negative : "");
        renderAIVariants(data.variants || []); $("result").hidden = false;
        $("engineStatus").textContent = `Generated locally with ${data.model || $("model").value}.`;
        $("result").scrollIntoView({ behavior:"smooth", block:"start" }); return true;
      } catch (error) {
        $("engineStatus").textContent = location.protocol === "file:" ? "Run run-local.ps1, then use http://localhost:8765." : `Local AI error: ${error.message}`;
        if ($("target").value !== "danbooru") return false;
      } finally { liveThinking.stop(); rollButton.disabled = false; rollButton.firstChild.textContent = "Enough — generate prompt "; }
    }
    state.tags = unique([
      ...presets.quality[$("quality").value],
      ...parseIdea(enrichedIdea),
      ...presets.framing[$("framing").value],
      ...presets.style[$("style").value],
      ...[$("camera").value, $("location").value, $("angle").value, $("pose").value, $("actors").value, $("interaction").value].filter(value => value !== "auto")
    ]);
    state.outputFormat = "danbooru";
    $("resultTitle").textContent = "Ready for Danbooru";
    render();
    renderVariants(enrichedIdea);
    $("negative").value = negative;
    $("result").hidden = false;
    $("result").scrollIntoView({ behavior:"smooth", block:"start" });
    return true;
  }

  function autoRegeneratePrompt() {
    if ($("result").hidden) return;
    clearTimeout(state.autoRegenerateTimer);
    $("engineStatus").textContent = "Setting changed — regenerating the prompt locally…";
    state.autoRegenerateTimer = setTimeout(() => {
      if ($("generateNow").disabled) { autoRegeneratePrompt(); return; }
      generatePrompt();
    }, 300);
  }

  function syncTargetControls() {
    const target = $("target").value;
    $("profileLabel").hidden = target !== "danbooru";
    $("h3Panel").hidden = target !== "minimax_h3";
    if (target === "krea2") $("style").value = "photo";
    if (target === "minimax_h3" && !$("scenes").children.length) resetScenes();
    syncH3Mode();
  }

  function syncH3Mode() { $("i2vPanel").hidden = $("target").value !== "minimax_h3" || $("h3Mode").value !== "i2v"; }

  function resetPrompt() {
    state.tags = []; state.interviewAnswers = []; state.interviewAsked = []; state.interviewAskedIds = []; state.refinementHistory = []; state.outputFormat = "danbooru";
    $("idea").value = "";
    ["camera", "location", "angle", "pose", "actors", "interaction"].forEach(id => $(id).value = "auto");
    $("style").value = $("target").value === "krea2" ? "photo" : "anime"; $("framing").value = "auto"; $("quality").value = "high";
    $("nsfwMode").checked = false;
    $("h3Mode").value = "t2v"; $("h3Format").value = "template"; $("h3Duration").value = "6"; $("referenceImage").value = ""; $("imageWorkspace").hidden = true; ["h3Dialogue","h3OnscreenText","h3Soundscape","h3Music","h3Extra"].forEach(id => $(id).value = ""); state.imageAnalysis = null; resetScenes(); syncH3Mode();
    $("questions").replaceChildren(); $("answerCount").textContent = "0 answered";
    $("thinkingPanel").hidden = true; $("thinkingPanel").open = false; $("thinkingText").textContent = "";
    $("askMore").hidden = false; $("askMore").textContent = "Ask next questions";
    $("interview").hidden = true; $("result").hidden = true; $("variantsCard").hidden = true;
    $("roll").hidden = false; $("roll").disabled = false; $("roll").firstChild.textContent = "Start prompt interview ";
    $("positive").value = ""; $("negative").value = ""; $("refinementFeedback").value = ""; $("refinementStatus").textContent = ""; $("resultTitle").textContent = "Ready for your workflow"; $("tags").replaceChildren();
    $("engineStatus").textContent = "Prompt reset. Enter a new idea when you’re ready.";
    $("idea").focus(); window.scrollTo({ top:0, behavior:"smooth" });
  }

  function saveRefinementFeedback() {
    const feedback = $("refinementFeedback").value.trim();
    if (!feedback) { $("refinementFeedback").focus(); return false; }
    state.refinementHistory.push(feedback);
    return true;
  }

  async function refinePrompt() {
    const feedback = $("refinementFeedback").value.trim();
    if (!feedback || !saveRefinementFeedback()) return;
    const previousPrompt = $("positive").value.trim();
    const cumulativeRevisions = state.refinementHistory.map((item, index) => `${index + 1}. ${item}`).join("\n");
    const button = $("refinePrompt"); button.disabled = true; button.firstChild.textContent = "Regenerating… ";
    $("refinementStatus").textContent = "Applying your feedback to a new local prompt…";
    try {
      const generated = await generatePrompt(cumulativeRevisions, previousPrompt);
      if (generated) {
        $("refinementFeedback").value = "";
        $("refinementStatus").textContent = `Feedback applied. ${state.refinementHistory.length} cumulative revision${state.refinementHistory.length === 1 ? "" : "s"} preserved.`;
      } else {
        state.refinementHistory.pop();
        $("refinementStatus").textContent = "The prompt was not regenerated. Check the local AI error shown above.";
      }
    } catch (error) {
      state.refinementHistory.pop();
      $("refinementStatus").textContent = `Regeneration failed: ${error.message}`;
    } finally {
      button.disabled = false; button.firstChild.textContent = "Apply feedback & regenerate ";
    }
  }

  async function refineWithQuestions() {
    if (!saveRefinementFeedback()) return;
    $("interview").hidden = false;
    $("interview").scrollIntoView({ behavior:"smooth", block:"start" });
    const button = $("refineQuestions"); button.disabled = true; button.textContent = "Reading feedback…";
    $("refinementStatus").textContent = "Ollama is preparing follow-up questions…";
    await requestQuestions(false);
    button.disabled = false; button.textContent = "Ask more questions";
    $("refinementStatus").textContent = "Follow-up questions are ready above.";
  }

  function renderAIVariants(variants) {
    const card = $("variantsCard"); card.hidden = !variants.length;
    if (!variants.length) return;
    $("variants").replaceChildren(...variants.map((variant, index) => {
      const natural = state.outputFormat !== "danbooru";
      const content = natural ? (variant.prompt || "") : unique(variant.tags || []);
      const button = document.createElement("button"); button.className = "variant";
      button.innerHTML = `${variant.name || `Variant ${index + 1}`}<small>${natural ? content : content.join(", ")}</small>`;
      button.addEventListener("click", () => { if (natural) $("positive").value = content; else { state.tags = content; render(); } }); return button;
    }));
  }

  function renderVariants(idea) {
    const wantsVariants = /multiple|camera angles?|different angles?|several (?:images|shots)/i.test(idea);
    const card = $("variantsCard"); card.hidden = !wantsVariants;
    if (!wantsVariants) return;
    const angles = [
      ["Front two-shot", ["front_view", "two-shot", "eye-level"]],
      ["Cozy side view", ["from_side", "medium_shot", "candid"]],
      ["Room-wide view", ["wide_shot", "interior", "establishing_shot"]],
      ["Popcorn close-up", ["close-up", "from_above", "food_focus"]]
    ];
    $("variants").replaceChildren(...angles.map(([label, camera]) => {
      const button = document.createElement("button"); button.className = "variant";
      button.innerHTML = `${label}<small>${camera.join(", ")}</small>`;
      button.addEventListener("click", () => {
        state.tags = unique([...state.tags.filter(t => !["front_view","two-shot","eye-level","from_side","medium_shot","candid","wide_shot","establishing_shot","close-up","from_above","food_focus"].includes(t)), ...camera]);
        render(); $("positive").scrollIntoView({ behavior:"smooth", block:"center" });
      });
      return button;
    }));
  }

  function render() {
    $("tagEditor").hidden = false;
    $("positive").value = state.tags.join(", ");
    $("tagCount").textContent = `${state.tags.length} tags`;
    $("tags").replaceChildren(...state.tags.map((tag, index) => {
      const chip = document.createElement("span"); chip.className = "tag";
      const text = document.createElement("span"); text.textContent = tag;
      const remove = document.createElement("button"); remove.textContent = "×"; remove.title = `Remove ${tag}`;
      remove.addEventListener("click", () => { state.tags.splice(index, 1); render(); });
      chip.append(text, remove); return chip;
    }));
  }

  function addTag(value = $("newTag").value) {
    const tag = normalizeTag(value);
    if (tag && !state.tags.includes(tag)) state.tags.push(tag);
    $("newTag").value = ""; $("suggestions").hidden = true; render();
  }
  async function copy(value, button) {
    await navigator.clipboard.writeText(value);
    const old = button.textContent; button.textContent = "Copied!"; setTimeout(() => button.textContent = old, 1200);
  }

  let suggestTimer;
  async function suggest() {
    clearTimeout(suggestTimer);
    const query = $("newTag").value.trim();
    if (query.length < 2) { $("suggestions").hidden = true; return; }
    suggestTimer = setTimeout(async () => {
      try {
        const url = `https://danbooru.donmai.us/autocomplete.json?search[type]=tag_query&search[query]=${encodeURIComponent(query)}&limit=6`;
        const response = await fetch(url); if (!response.ok) throw new Error();
        const data = await response.json();
        const box = $("suggestions"); box.replaceChildren(...data.map((item) => {
          const button = document.createElement("button");
          const name = item.value || item.label || item;
          button.innerHTML = `<span>${String(name).replace(/</g,"&lt;")}</span><small>${item.post_count ? Number(item.post_count).toLocaleString() : ""}</small>`;
          button.addEventListener("click", () => addTag(name)); return button;
        })); box.hidden = !data.length;
      } catch { $("suggestions").hidden = true; }
    }, 280);
  }

  $("roll").addEventListener("click", () => requestQuestions(true));
  $("askMore").addEventListener("click", () => requestQuestions(false));
  $("generateNow").addEventListener("click", generatePrompt);
  $("resetPrompt").addEventListener("click", resetPrompt);
  $("refinePrompt").addEventListener("click", refinePrompt);
  $("refineQuestions").addEventListener("click", refineWithQuestions);
  $("target").addEventListener("change", () => {
    syncTargetControls();
    if ($("result").hidden) loadExamples();
  });
  $("nsfwMode").addEventListener("change", () => { if ($("result").hidden) loadExamples(); });
  $("showThinking").addEventListener("change", () => {
    if (!$("showThinking").checked) { $("thinkingPanel").hidden = true; $("thinkingPanel").open = false; }
  });
  ["engine", "model", "target", "checkpointProfile", "style", "framing", "quality", "camera", "location", "angle", "pose", "actors", "interaction", "nsfwMode", "h3Mode", "h3Format", "h3Duration", "visionModel", "h3Dialogue", "h3OnscreenText", "h3Soundscape", "h3Music", "h3Extra"].forEach(id => $(id).addEventListener("change", autoRegeneratePrompt));
  $("h3Mode").addEventListener("change", syncH3Mode);
  $("h3Duration").addEventListener("change", () => { const duration=Number($("h3Duration").value); [...document.querySelectorAll(".scene-end")].forEach(input => { if (Number(input.value) > duration) input.value=duration; }); });
  $("addScene").addEventListener("click", () => { const cards=[...$("scenes").children]; const last=cards.length ? readScene(cards[cards.length-1]) : {end:0}; $("scenes").append(sceneTemplate({start:Math.min(last.end, Number($("h3Duration").value)), end:Number($("h3Duration").value)})); numberScenes(); });
  $("referenceImage").addEventListener("change", event => analyzeReferenceImage(event.target.files[0]));
  $("themeToggle").addEventListener("click", () => setTheme(document.documentElement.dataset.theme === "dark" ? "light" : "dark"));
  $("slogan").addEventListener("click", rollSlogan);
  $("addTag").addEventListener("click", () => addTag());
  $("newTag").addEventListener("input", suggest);
  $("newTag").addEventListener("keydown", (e) => { if (e.key === "Enter") { e.preventDefault(); addTag(); } });
  $("copyPositive").addEventListener("click", () => copy($("positive").value, $("copyPositive")));
  $("copyNegative").addEventListener("click", () => copy($("negative").value, $("copyNegative")));
  $("copyAll").addEventListener("click", () => copy(`POSITIVE\n${$("positive").value}\n\nNEGATIVE\n${$("negative").value}`, $("copyAll")));
  $("positive").addEventListener("change", () => { state.tags = unique($("positive").value.split(",")); render(); });
  document.querySelectorAll("[data-example]").forEach((button) => button.addEventListener("click", () => { $("idea").value = button.dataset.example; $("idea").focus(); }));
  async function initialize() {
    try {
      const response = await fetch("/api/config", { cache:"no-store" });
      if (response.ok) {
        const config = await response.json();
        if (config.defaultModel) $("model").value = config.defaultModel;
        if (config.visionModel) $("visionModel").value = config.visionModel;
      }
    } catch { /* Keep the bundled defaults when running without the .NET host. */ }
    syncTargetControls();
    loadExamples();
  }
  initialize();
})();
