import "./styles.css";

type State = "home" | "calibrating" | "ready" | "playing" | "gameover";

type AudioSnapshot = {
  energy: number;
  bass: number;
  brightness: number;
  flux: number;
  confidence: number;
  peak: boolean;
};

type HazardKind = "bass" | "spark" | "shock";

type Hazard = {
  x: number;
  y: number;
  width: number;
  height: number;
  vx: number;
  vy: number;
  life: number;
  kind: HazardKind;
};

class AudioEngine {
  private context: AudioContext | null = null;
  private analyser: AnalyserNode | null = null;
  private source: MediaStreamAudioSourceNode | null = null;
  private stream: MediaStream | null = null;
  private timeData = new Float32Array(1024);
  private freqData = new Uint8Array(512);
  private lastSpectrum = new Float32Array(512);
  private lastPeakAt = 0;
  private smoothedEnergy = 0;
  private smoothedBass = 0;
  private smoothedBrightness = 0;
  private noiseFloor = 0;
  private calibrationFrames: AudioSnapshot[] = [];

  async request(): Promise<void> {
    if (this.stream) {
      return;
    }

    this.stream = await navigator.mediaDevices.getUserMedia({
      audio: {
        echoCancellation: false,
        noiseSuppression: false,
        autoGainControl: false,
      },
      video: false,
    });

    this.context = new AudioContext();
    this.analyser = this.context.createAnalyser();
    this.analyser.fftSize = 1024;
    this.analyser.smoothingTimeConstant = 0.55;
    this.source = this.context.createMediaStreamSource(this.stream);
    this.source.connect(this.analyser);
  }

  async resume(): Promise<void> {
    if (this.context && this.context.state === "suspended") {
      await this.context.resume();
    }
  }

  resetCalibration(): void {
    this.calibrationFrames = [];
  }

  sample(now: number): AudioSnapshot {
    if (!this.analyser) {
      return {
        energy: 0,
        bass: 0,
        brightness: 0,
        flux: 0,
        confidence: 0,
        peak: false,
      };
    }

    this.analyser.getFloatTimeDomainData(this.timeData);
    this.analyser.getByteFrequencyData(this.freqData);

    let sumSquares = 0;
    let bass = 0;
    let brightness = 0;
    let flux = 0;

    for (let i = 0; i < this.timeData.length; i += 1) {
      const sample = this.timeData[i];
      sumSquares += sample * sample;
    }

    const bassBins = Math.max(6, Math.floor(this.freqData.length * 0.08));
    const highStart = Math.floor(this.freqData.length * 0.45);

    for (let i = 0; i < this.freqData.length; i += 1) {
      const value = this.freqData[i] / 255;
      if (i < bassBins) {
        bass += value;
      }
      if (i >= highStart) {
        brightness += value;
      }

      const delta = value - this.lastSpectrum[i];
      if (delta > 0) {
        flux += delta;
      }
      this.lastSpectrum[i] = value;
    }

    const rawEnergy = Math.sqrt(sumSquares / this.timeData.length);
    const rawBass = bass / bassBins;
    const rawBrightness = brightness / (this.freqData.length - highStart);
    const rawFlux = flux / this.freqData.length;

    this.smoothedEnergy = lerp(this.smoothedEnergy, rawEnergy, 0.16);
    this.smoothedBass = lerp(this.smoothedBass, rawBass, 0.18);
    this.smoothedBrightness = lerp(this.smoothedBrightness, rawBrightness, 0.18);
    this.noiseFloor = this.noiseFloor === 0 ? rawEnergy : lerp(this.noiseFloor, Math.min(this.noiseFloor, rawEnergy), 0.03);

    const confidence = clamp01(
      this.smoothedEnergy * 11 + this.smoothedBass * 0.8 + rawFlux * 14,
    );
    const peak =
      now - this.lastPeakAt > 280 &&
      rawEnergy > Math.max(this.noiseFloor * 2.4, 0.035) &&
      rawFlux > 0.008;

    if (peak) {
      this.lastPeakAt = now;
    }

    const snapshot = {
      energy: clamp01(this.smoothedEnergy * 9),
      bass: clamp01(this.smoothedBass * 2.4),
      brightness: clamp01(this.smoothedBrightness * 2.1),
      flux: clamp01(rawFlux * 22),
      confidence,
      peak,
    };

    this.calibrationFrames.push(snapshot);
    if (this.calibrationFrames.length > 180) {
      this.calibrationFrames.shift();
    }

    return snapshot;
  }

  calibration() {
    if (this.calibrationFrames.length === 0) {
      return {
        quality: "fallback" as const,
        message: "No signal detected yet.",
        energy: 0,
        confidence: 0,
        peakEnergy: 0,
        activeRatio: 0,
      };
    }

    const totals = this.calibrationFrames.reduce(
      (acc, frame) => {
        acc.energy += frame.energy;
        acc.confidence += frame.confidence;
        acc.peakEnergy = Math.max(acc.peakEnergy, frame.energy);
        if (frame.energy >= 0.04 || frame.confidence >= 0.12) {
          acc.activeFrames += 1;
        }
        if (frame.peak) {
          acc.peaks += 1;
        }
        return acc;
      },
      { energy: 0, confidence: 0, peaks: 0, peakEnergy: 0, activeFrames: 0 },
    );

    const energy = totals.energy / this.calibrationFrames.length;
    const confidence = totals.confidence / this.calibrationFrames.length;
    const activeRatio = totals.activeFrames / this.calibrationFrames.length;
    const peakEnergy = totals.peakEnergy;

    if ((energy >= 0.1 && confidence >= 0.22) || (peakEnergy >= 0.22 && activeRatio >= 0.3)) {
      return {
        quality: "usable" as const,
        message: "Signal is strong. Start the run.",
        energy,
        confidence,
        peakEnergy,
        activeRatio,
      };
    }

    if ((energy >= 0.04 && confidence >= 0.1) || activeRatio >= 0.18 || peakEnergy >= 0.12) {
      return {
        quality: "weak" as const,
        message: "Playable, but louder music should feel better.",
        energy,
        confidence,
        peakEnergy,
        activeRatio,
      };
    }

    return {
      quality: "fallback" as const,
      message: "Too quiet. Raise volume, move closer, or use an external speaker.",
      energy,
      confidence,
      peakEnergy,
      activeRatio,
    };
  }
}

class Game {
  private readonly canvas: HTMLCanvasElement;
  private readonly ctx: CanvasRenderingContext2D;
  private readonly audio: AudioEngine;
  private readonly scoreValue: HTMLElement;
  private readonly bestValue: HTMLElement;
  private readonly statusValue: HTMLElement;
  private readonly overlayTitle: HTMLElement;
  private readonly overlayBody: HTMLElement;
  private readonly overlayMetrics: HTMLElement;
  private readonly actionPrimary: HTMLButtonElement;
  private readonly actionSecondary: HTMLButtonElement;

  private state: State = "home";
  private width = 0;
  private height = 0;
  private pointerX = 0.5;
  private playerX = 0.5;
  private readonly playerY = 0.88;
  private hazards: Hazard[] = [];
  private score = 0;
  private best = Number(localStorage.getItem("song-survival.best") ?? "0");
  private startedAt = 0;
  private lastAt = 0;
  private lastBassSpawn = 0;
  private lastSparkSpawn = 0;
  private lastShockSpawn = 0;
  private currentAudio: AudioSnapshot = {
    energy: 0,
    bass: 0,
    brightness: 0,
    flux: 0,
    confidence: 0,
    peak: false,
  };
  private calibrationDoneAt = 0;
  private calibrationQuality: "unknown" | "usable" | "weak" | "fallback" = "unknown";
  private readonly overlayRoot: HTMLElement;
  private readonly supportsMic: boolean;
  private readonly isSecure: boolean;

  constructor(root: HTMLElement) {
    root.innerHTML = `
      <main class="shell">
        <header class="topbar">
          <div class="brand-lockup">
            <p class="brand-kicker">A music-reactive web game</p>
            <p class="brand-credit">Created by אורי גליקמן</p>
          </div>
          <div class="brand-pill">iPhone Safari</div>
        </header>
        <section class="hud">
          <div class="stat"><span>Score</span><strong id="score">0.0</strong></div>
          <div class="stat"><span>Best</span><strong id="best">0.0</strong></div>
          <div class="stat stat-wide"><span>Audio</span><strong id="status">Waiting for microphone</strong></div>
        </section>
        <section class="stage-wrap">
          <canvas id="game"></canvas>
          <div class="overlay" id="overlay">
            <div class="card">
              <div class="cover-art" aria-hidden="true">
                <div class="cover-ring cover-ring-a"></div>
                <div class="cover-ring cover-ring-b"></div>
                <div class="cover-wave"></div>
                <div class="cover-face">
                  <span></span>
                  <span></span>
                </div>
              </div>
              <p class="eyebrow">Song Survival</p>
              <h1 id="overlay-title">Survive inside the song</h1>
              <p id="overlay-body">Open this in Safari on iPhone, play music nearby, then let the browser use the microphone.</p>
              <p id="overlay-metrics" class="metrics">Live signal will appear here once the mic is enabled.</p>
              <p class="micro-copy">Best with speaker audio in the room. Headphones will usually make the game hear nothing.</p>
              <div class="actions">
                <button id="primary">Enable microphone</button>
                <button id="secondary" class="secondary">Retry calibration</button>
              </div>
            </div>
          </div>
        </section>
        <footer class="footer-note">
          <span>Every song becomes a different survival run.</span>
          <span>Share the link. Try another track. Beat your best.</span>
        </footer>
      </main>
    `;

    this.canvas = root.querySelector<HTMLCanvasElement>("#game")!;
    this.ctx = this.canvas.getContext("2d")!;
    this.audio = new AudioEngine();
    this.scoreValue = root.querySelector("#score")!;
    this.bestValue = root.querySelector("#best")!;
    this.statusValue = root.querySelector("#status")!;
    this.overlayTitle = root.querySelector("#overlay-title")!;
    this.overlayBody = root.querySelector("#overlay-body")!;
    this.overlayMetrics = root.querySelector("#overlay-metrics")!;
    this.actionPrimary = root.querySelector<HTMLButtonElement>("#primary")!;
    this.actionSecondary = root.querySelector<HTMLButtonElement>("#secondary")!;
    this.overlayRoot = root.querySelector<HTMLElement>("#overlay")!;
    this.supportsMic = Boolean(navigator.mediaDevices?.getUserMedia);
    this.isSecure =
      window.location.protocol === "https:" ||
      window.location.hostname === "localhost" ||
      window.location.hostname === "127.0.0.1";

    this.bestValue.textContent = this.best.toFixed(1);
    this.actionPrimary.addEventListener("click", () => this.handlePrimary());
    this.actionSecondary.addEventListener("click", () => this.beginCalibration());
    this.canvas.addEventListener("pointerdown", this.onPointer);
    this.canvas.addEventListener("pointermove", this.onPointer);
    window.addEventListener("resize", () => this.resize());
    document.addEventListener("visibilitychange", this.onVisibilityChange);
    this.resize();
    this.bootstrapOverlay();
    requestAnimationFrame((t) => this.frame(t));
  }

  private onPointer = (event: PointerEvent) => {
    const rect = this.canvas.getBoundingClientRect();
    this.pointerX = clamp01((event.clientX - rect.left) / rect.width);
  };

  private async handlePrimary() {
    if (!this.supportsMic) {
      this.setOverlay(
        "Browser not supported",
        "This browser does not expose microphone access. Open the link in Safari on iPhone or a modern Chromium browser.",
        "Open in supported browser",
        false,
      );
      return;
    }

    if (!this.isSecure) {
      this.setOverlay(
        "HTTPS required",
        "Microphone access only works on HTTPS pages or localhost. Open the deployed Vercel link instead of a raw local file.",
        "OK",
        false,
      );
      return;
    }

    if (this.state === "home") {
      try {
        await this.audio.request();
        await this.audio.resume();
        this.beginCalibration();
      } catch (error) {
        this.setOverlay(
          "Microphone blocked",
          "Safari needs microphone access for this game. Allow it and try again.",
          "Enable microphone",
          true,
        );
      }
      return;
    }

    if (this.state === "ready" || this.state === "gameover") {
      this.startRun();
    }
  }

  private beginCalibration() {
    this.audio.resetCalibration();
    this.calibrationDoneAt = performance.now() + 3000;
    this.state = "calibrating";
    this.setOverlay(
      "Listening to the room",
      "Keep the music playing for a few seconds. Same-device playback on iPhone is best effort, so louder volume helps.",
      "Calibrating...",
      false,
    );
  }

  private startRun() {
    this.state = "playing";
    this.hazards = [];
    this.score = 0;
    this.startedAt = performance.now();
    this.lastBassSpawn = this.startedAt;
    this.lastSparkSpawn = this.startedAt;
    this.lastShockSpawn = this.startedAt;
    this.hideOverlay();
  }

  private endRun() {
    this.state = "gameover";
    if (this.score > this.best) {
      this.best = this.score;
      localStorage.setItem("song-survival.best", this.best.toFixed(1));
      this.bestValue.textContent = this.best.toFixed(1);
    }

    this.setOverlay(
      "Run over",
      `Score ${this.score.toFixed(1)}. Send the link, play another song, and try again.`,
      "Replay",
      true,
    );
  }

  private onVisibilityChange = () => {
    if (document.hidden && this.state === "playing") {
      this.state = "ready";
      this.setOverlay(
        "Paused",
        "The run paused when the page lost focus. Tap Start run to continue with a fresh run.",
        "Start run",
        true,
      );
    }
  };

  private frame(now: number) {
    const delta = this.lastAt === 0 ? 16 : now - this.lastAt;
    this.lastAt = now;
    this.currentAudio = this.audio.sample(now);
    this.tickState(now, delta);
    this.render();
    requestAnimationFrame((t) => this.frame(t));
  }

  private tickState(now: number, delta: number) {
    if (this.state === "calibrating" && now >= this.calibrationDoneAt) {
      const result = this.audio.calibration();
      this.calibrationQuality = result.quality;
      this.state = "ready";
      this.setOverlay(
        result.quality === "fallback" ? "Need stronger audio" : "Ready",
        result.message,
        "Start run",
        true,
      );
      this.overlayMetrics.textContent =
        `Avg ${result.energy.toFixed(2)} • Peak ${result.peakEnergy.toFixed(2)} • Active ${Math.round(result.activeRatio * 100)}%`;
      this.actionPrimary.textContent = "Start run";
      this.actionSecondary.textContent = "Recalibrate";
    }

    this.statusValue.textContent = this.buildStatus();
    this.updateOverlayMetrics();

    if (this.state !== "playing") {
      return;
    }

    const elapsed = (now - this.startedAt) / 1000;
    const timeRamp = clamp01(elapsed / 70);
    const danger = clamp01(this.currentAudio.energy * 0.58 + this.currentAudio.brightness * 0.16 + timeRamp * 0.34);
    const worldSpeed = 200 + danger * 260;
    const bassPressure = clamp01(this.currentAudio.bass * 0.76 + timeRamp * 0.2);
    const sparkPressure = clamp01(this.currentAudio.brightness * 0.7 + this.currentAudio.flux * 0.4 + timeRamp * 0.15);

    this.playerX = lerp(this.playerX, this.pointerX, 0.24);
    this.score += (delta / 1000) * (1 + danger * 0.35);
    this.scoreValue.textContent = this.score.toFixed(1);

    const bassInterval = lerp(1800, 520, bassPressure);
    const sparkInterval = lerp(620, 120, sparkPressure);
    const shockCooldown = lerp(2100, 900, timeRamp);

    if (now - this.lastBassSpawn >= bassInterval) {
      this.spawnBass(worldSpeed, bassPressure);
      this.lastBassSpawn = now;
    }

    if (now - this.lastSparkSpawn >= sparkInterval) {
      this.spawnSpark(worldSpeed, sparkPressure);
      this.lastSparkSpawn = now;
    }

    if (this.currentAudio.peak && now - this.lastShockSpawn >= shockCooldown) {
      this.spawnShock(worldSpeed);
      this.lastShockSpawn = now;
    }

    this.updateHazards(delta, worldSpeed);
  }

  private updateHazards(delta: number, worldSpeed: number) {
    const player = {
      x: this.playerX * this.width,
      y: this.playerY * this.height,
      r: Math.max(16, this.width * 0.024),
    };

    this.hazards = this.hazards.filter((hazard) => {
      hazard.x += hazard.vx * (delta / 1000);
      hazard.y += hazard.vy * (delta / 1000) * (worldSpeed / 260);
      hazard.life -= delta;

      const collision =
        player.x + player.r > hazard.x - hazard.width / 2 &&
        player.x - player.r < hazard.x + hazard.width / 2 &&
        player.y + player.r > hazard.y - hazard.height / 2 &&
        player.y - player.r < hazard.y + hazard.height / 2;

      if (collision) {
        this.endRun();
        return false;
      }

      return hazard.life > 0 && hazard.y < this.height + 120;
    });
  }

  private spawnBass(worldSpeed: number, pressure: number) {
    const width = lerp(this.width * 0.26, this.width * 0.58, pressure);
    const x = lerp(width / 2 + 12, this.width - width / 2 - 12, Math.random());
    this.hazards.push({
      kind: "bass",
      x,
      y: -40,
      width,
      height: 24,
      vx: 0,
      vy: worldSpeed * 0.88,
      life: 5000,
    });
  }

  private spawnSpark(worldSpeed: number, pressure: number) {
    this.hazards.push({
      kind: "spark",
      x: lerp(24, this.width - 24, Math.random()),
      y: -20,
      width: lerp(14, 24, pressure),
      height: lerp(20, 56, pressure),
      vx: lerp(-24, 24, Math.random()),
      vy: worldSpeed * 1.1,
      life: 4200,
    });
  }

  private spawnShock(worldSpeed: number) {
    this.hazards.push({
      kind: "shock",
      x: this.width / 2,
      y: -24,
      width: this.width * 0.88,
      height: 16,
      vx: 0,
      vy: worldSpeed,
      life: 4200,
    });
  }

  private render() {
    const bg = this.ctx.createLinearGradient(0, 0, 0, this.height);
    bg.addColorStop(0, colorMix("#1d2730", "#43212a", this.currentAudio.energy));
    bg.addColorStop(1, colorMix("#0f1318", "#1a1f36", this.currentAudio.brightness));
    this.ctx.fillStyle = bg;
    this.ctx.fillRect(0, 0, this.width, this.height);

    this.drawPulseField();
    this.drawHazards();
    this.drawPlayer();
  }

  private drawPulseField() {
    const ringCount = 4;
    for (let i = 0; i < ringCount; i += 1) {
      const offset = ((performance.now() * 0.05 + i * 130) % (this.height + 240)) - 120;
      this.ctx.strokeStyle = `rgba(255,255,255,${0.08 + this.currentAudio.energy * 0.1})`;
      this.ctx.lineWidth = 2;
      this.ctx.beginPath();
      this.ctx.ellipse(this.width / 2, offset, this.width * (0.16 + i * 0.08), 22 + this.currentAudio.bass * 24, 0, 0, Math.PI * 2);
      this.ctx.stroke();
    }
  }

  private drawHazards() {
    for (const hazard of this.hazards) {
      if (hazard.kind === "bass") {
        this.ctx.fillStyle = "rgba(112, 214, 255, 0.9)";
      } else if (hazard.kind === "spark") {
        this.ctx.fillStyle = "rgba(255, 224, 114, 0.95)";
      } else {
        this.ctx.fillStyle = "rgba(255, 108, 153, 0.95)";
      }

      this.ctx.beginPath();
      this.ctx.roundRect(
        hazard.x - hazard.width / 2,
        hazard.y - hazard.height / 2,
        hazard.width,
        hazard.height,
        14,
      );
      this.ctx.fill();
    }
  }

  private drawPlayer() {
    const x = this.playerX * this.width;
    const y = this.playerY * this.height;
    const body = Math.max(18, this.width * 0.028);

    this.ctx.fillStyle = "#ffd4df";
    this.ctx.beginPath();
    this.ctx.arc(x, y, body, 0, Math.PI * 2);
    this.ctx.fill();

    this.ctx.fillStyle = "#2f2330";
    this.ctx.beginPath();
    this.ctx.arc(x - body * 0.35, y - body * 0.15, body * 0.12, 0, Math.PI * 2);
    this.ctx.arc(x + body * 0.35, y - body * 0.15, body * 0.12, 0, Math.PI * 2);
    this.ctx.fill();
  }

  private buildStatus() {
    if (this.state === "calibrating") {
      return `Listening ${Math.max(0, Math.ceil((this.calibrationDoneAt - performance.now()) / 1000))}`;
    }

    if (this.currentAudio.confidence >= 0.55) {
      return `Strong ${this.currentAudio.confidence.toFixed(2)}`;
    }
    if (this.currentAudio.confidence >= 0.22) {
      return `Weak ${this.currentAudio.confidence.toFixed(2)}`;
    }
    return `Quiet ${this.currentAudio.confidence.toFixed(2)}`;
  }

  private setOverlay(title: string, body: string, primary: string, showSecondary: boolean) {
    this.overlayTitle.textContent = title;
    this.overlayBody.textContent = body;
    this.actionPrimary.textContent = primary;
    this.actionSecondary.hidden = !showSecondary;
    this.overlayRoot.classList.remove("hidden");
  }

  private hideOverlay() {
    this.overlayRoot.classList.add("hidden");
  }

  private resize() {
    const rect = this.canvas.parentElement!.getBoundingClientRect();
    const dpr = Math.min(window.devicePixelRatio || 1, 2);
    this.width = rect.width;
    this.height = rect.height;
    this.canvas.width = rect.width * dpr;
    this.canvas.height = rect.height * dpr;
    this.canvas.style.width = `${rect.width}px`;
    this.canvas.style.height = `${rect.height}px`;
    this.ctx.setTransform(dpr, 0, 0, dpr, 0, 0);
  }

  private bootstrapOverlay() {
    this.actionSecondary.hidden = true;

    if (!this.supportsMic) {
      this.setOverlay(
        "Browser not supported",
        "Open this link in Safari on iPhone or a modern browser with microphone support.",
        "OK",
        false,
      );
      return;
    }

    if (!this.isSecure) {
      this.setOverlay(
        "HTTPS required",
        "Microphone access only works on HTTPS pages or localhost. Use the Vercel link for phone testing.",
        "OK",
        false,
      );
      return;
    }

    this.setOverlay(
      "Survive inside the song",
      "Tap Enable microphone, keep music playing nearby, then drag left and right to dodge sound waves.",
      "Enable microphone",
      false,
    );
    this.overlayMetrics.textContent = "Live signal will appear here once the mic is enabled.";
  }

  private updateOverlayMetrics() {
    if (this.state === "playing") {
      return;
    }

    this.overlayMetrics.textContent =
      `Live ${this.currentAudio.energy.toFixed(2)} • Bass ${this.currentAudio.bass.toFixed(2)} • Confidence ${this.currentAudio.confidence.toFixed(2)}`;
  }
}

function clamp01(value: number) {
  return Math.min(1, Math.max(0, value));
}

function lerp(a: number, b: number, t: number) {
  return a + (b - a) * t;
}

function colorMix(a: string, b: string, t: number) {
  const ar = Number.parseInt(a.slice(1, 3), 16);
  const ag = Number.parseInt(a.slice(3, 5), 16);
  const ab = Number.parseInt(a.slice(5, 7), 16);
  const br = Number.parseInt(b.slice(1, 3), 16);
  const bg = Number.parseInt(b.slice(3, 5), 16);
  const bb = Number.parseInt(b.slice(5, 7), 16);
  return `rgb(${Math.round(lerp(ar, br, t))} ${Math.round(lerp(ag, bg, t))} ${Math.round(lerp(ab, bb, t))})`;
}

new Game(document.querySelector("#app")!);
