const API_KEY_STORAGE = "strategyApiKey";
const REFRESH_INTERVAL_MS = 15000;
const POLL_INTERVAL_MS = 3000;
const POLL_TIMEOUT_MS = 60000;
const TERMINAL_STATUSES = new Set(["filled", "error", "expired", "cancelled"]);

const el = (id) => document.getElementById(id);

function getApiKey() {
  return localStorage.getItem(API_KEY_STORAGE) || "";
}

function fmtSigned(n) {
  if (n === null || n === undefined) return "0.00";
  const v = Number(n);
  const sign = v > 0 ? "+" : v < 0 ? "-" : "";
  return `${sign}${Math.abs(v).toFixed(2)}`;
}

function fmtQty(n) {
  if (n === null || n === undefined) return "";
  return Number(n).toFixed(4).replace(/\.?0+$/, "") || "0";
}

async function apiGet(path) {
  const res = await fetch(path, { headers: { Accept: "application/json" } });
  if (!res.ok) throw new Error(`${path} -> HTTP ${res.status}`);
  return res.json();
}

async function apiSend(method, path, body) {
  const res = await fetch(path, {
    method,
    headers: {
      "Content-Type": "application/json",
      "X-Api-Key": getApiKey(),
    },
    body: body !== undefined ? JSON.stringify(body) : undefined,
  });
  let payload = null;
  try { payload = await res.json(); } catch { /* no body */ }
  if (!res.ok) {
    const message = payload?.error || `HTTP ${res.status}`;
    throw new Error(message);
  }
  return payload;
}

function clear(node) {
  while (node.firstChild) node.removeChild(node.firstChild);
}

function showToast(message, isError) {
  const toast = el("toast");
  toast.textContent = message;
  toast.classList.toggle("error", !!isError);
  toast.classList.remove("hidden");
  clearTimeout(showToast._t);
  showToast._t = setTimeout(() => toast.classList.add("hidden"), 5000);
}

// ---- Rendering ----

function renderHealth(health) {
  const dot = el("statusDot");
  const text = el("statusText");
  const detail = el("statusDetail");
  const pills = el("pills");

  dot.classList.toggle("running", !!health.daemonRunning);
  text.textContent = health.daemonRunning ? "Daemon Running" : "Daemon Stopped";

  if (!health.daemonRunning && typeof health.heartbeatAgeSeconds === "number") {
    const age = health.heartbeatAgeSeconds;
    const formatted = age >= 60 ? `${Math.floor(age / 60)}m ${age % 60}s` : `${age}s`;
    detail.textContent = `Last seen ${formatted} ago`;
  } else {
    detail.textContent = "";
  }

  clear(pills);
  const addPill = (label, cls) => {
    const p = document.createElement("span");
    p.className = `pill ${cls}`;
    p.textContent = label;
    pills.appendChild(p);
  };
  if (health.dryRun) addPill("PAPER TRADING", "paper");
  if (health.haltNewEntries) addPill("ENTRIES HALTED", "halted");
  if (health.pausedByUser) addPill("BUYING PAUSED", "paused");

  el("pauseBtn").classList.toggle("hidden", !!health.pausedByUser);
  el("resumeBtn").classList.toggle("hidden", !health.pausedByUser);
}

function renderPositions(positions) {
  const list = el("positionsList");
  clear(list);

  if (!positions.length) {
    const note = document.createElement("div");
    note.className = "empty-note";
    note.textContent = "No open positions";
    list.appendChild(note);
    return;
  }

  for (const pos of positions) {
    const card = document.createElement("div");
    card.className = "card position-card";

    const topRow = document.createElement("div");
    topRow.className = "top-row";

    const tickerGroup = document.createElement("div");
    tickerGroup.className = "ticker-group";
    const ticker = document.createElement("span");
    ticker.className = "ticker";
    ticker.textContent = pos.ticker;
    const currencyPill = document.createElement("span");
    currencyPill.className = "pill";
    currencyPill.style.background = "var(--pill-bg)";
    currencyPill.style.color = "var(--accent)";
    currencyPill.textContent = pos.currency || "";
    tickerGroup.append(ticker, currencyPill);

    const pnl = document.createElement("span");
    const pnlValue = pos.unrealizedPnl;
    pnl.className = `pnl ${pnlValue > 0 ? "profit" : pnlValue < 0 ? "loss" : ""}`;
    pnl.textContent = fmtSigned(pnlValue);

    topRow.append(tickerGroup, pnl);

    const metricsRow = document.createElement("div");
    metricsRow.className = "metrics-row";
    const metric = (label, value) => {
      const wrap = document.createElement("div");
      const l = document.createElement("div");
      l.className = "metric-label";
      l.textContent = label;
      const v = document.createElement("div");
      v.className = "metric-value";
      v.textContent = value;
      wrap.append(l, v);
      return wrap;
    };
    metricsRow.append(
      metric("Qty", fmtQty(pos.quantity)),
      metric("Entry", Number(pos.fillPrice).toFixed(2)),
      metric("Current", pos.currentPrice != null ? Number(pos.currentPrice).toFixed(2) : "-")
    );

    const sellBtn = document.createElement("button");
    sellBtn.className = "btn primary";
    sellBtn.textContent = "Sell Position";
    sellBtn.addEventListener("click", () => sellPosition(pos.ticker, sellBtn));

    card.append(topRow, metricsRow, sellBtn);
    list.appendChild(card);
  }
}

function renderCommands(commands) {
  const header = el("pendingHeader");
  const list = el("pendingList");
  clear(list);

  header.classList.toggle("hidden", commands.length === 0);
  if (!commands.length) return;

  for (const cmd of commands) {
    const card = document.createElement("div");
    card.className = "card command-card";

    const left = document.createElement("div");
    const title = document.createElement("div");
    title.className = "cmd-title";
    title.textContent = cmd.action || "";
    if (cmd.ticker) {
      const t = document.createElement("span");
      t.className = "ticker";
      t.textContent = cmd.ticker;
      title.appendChild(t);
    }
    const status = document.createElement("div");
    status.className = "caption";
    status.textContent = `Status: ${cmd.status}`;
    const requested = document.createElement("div");
    requested.className = "caption";
    const requestedDate = new Date(cmd.requestedAtUtc);
    requested.textContent = `Requested: ${requestedDate.toLocaleString(undefined, {
      month: "short", day: "numeric", hour: "2-digit", minute: "2-digit",
    })}`;
    left.append(title, status, requested);

    const cancelBtn = document.createElement("button");
    cancelBtn.className = "btn secondary";
    cancelBtn.textContent = "Cancel";
    cancelBtn.addEventListener("click", () => cancelCommand(cmd.id, cancelBtn));

    card.append(left, cancelBtn);
    list.appendChild(card);
  }
}

function renderTrades(trades) {
  const list = el("tradesList");
  clear(list);

  if (!trades.length) {
    const note = document.createElement("div");
    note.className = "empty-note";
    note.textContent = "No recent trades";
    list.appendChild(note);
    return;
  }

  for (const trade of trades) {
    const card = document.createElement("div");
    card.className = "card trade-card";

    const left = document.createElement("div");
    const ticker = document.createElement("div");
    ticker.className = "ticker";
    ticker.textContent = trade.ticker;
    const closed = document.createElement("div");
    closed.className = "caption";
    const closedDate = new Date(trade.dateClosed);
    closed.textContent = `Closed ${closedDate.toLocaleDateString(undefined, {
      day: "numeric", month: "short", year: "numeric",
    })}`;
    left.append(ticker, closed);

    const pnl = document.createElement("span");
    const pnlValue = trade.roundtripPnl;
    pnl.className = `pnl ${pnlValue > 0 ? "profit" : pnlValue < 0 ? "loss" : ""}`;
    pnl.textContent = fmtSigned(pnlValue);

    card.append(left, pnl);
    list.appendChild(card);
  }
}

// ---- Data loading ----

async function refreshAll() {
  el("statusMessage").textContent = "Loading...";
  try {
    const [health, positions, trades, commands] = await Promise.all([
      apiGet("/api/health"),
      apiGet("/api/positions"),
      apiGet("/api/trades/recent?count=20"),
      apiGet("/api/trades/commands").catch(() => []),
    ]);
    renderHealth(health);
    renderPositions(positions);
    renderTrades(trades);
    renderCommands(commands);
    el("statusMessage").textContent = "Updated";
  } catch (err) {
    console.error(err);
    el("statusMessage").textContent = "Error: Cannot reach server";
  }
}

async function pollCommand(id) {
  const start = Date.now();
  while (Date.now() - start < POLL_TIMEOUT_MS) {
    try {
      const cmd = await apiGet(`/api/trades/commands/${encodeURIComponent(id)}`);
      if (TERMINAL_STATUSES.has(cmd.status)) return cmd;
    } catch (err) {
      console.error("poll error", err);
    }
    await new Promise((r) => setTimeout(r, POLL_INTERVAL_MS));
  }
  return null;
}

// ---- Actions ----

async function withButtonDisabled(button, fn) {
  button.disabled = true;
  try {
    await fn();
  } finally {
    button.disabled = false;
  }
}

async function sellPosition(ticker, button) {
  await withButtonDisabled(button, async () => {
    try {
      const response = await apiSend("POST", "/api/trades/sell", { ticker });
      showToast(response.message || "Sell queued");
      await refreshAll();
      if (response.id) {
        const final = await pollCommand(response.id);
        if (final) {
          showToast(
            final.status === "filled" ? `Filled at ${Number(final.fillPrice).toFixed(2)}`
              : final.status === "error" ? (final.errorMessage || "Sell failed")
              : `Sell ${final.status}`,
            final.status === "error"
          );
        }
        await refreshAll();
      }
    } catch (err) {
      showToast(err.message, true);
    }
  });
}

async function sellAll() {
  const button = el("sellAllBtn");
  await withButtonDisabled(button, async () => {
    try {
      const response = await apiSend("POST", "/api/trades/sell-all");
      showToast(response.message || "Sell all queued");
      await refreshAll();
      if (response.id) {
        const final = await pollCommand(response.id);
        if (final) {
          showToast(
            final.status === "filled" ? "Filled"
              : final.status === "error" ? (final.errorMessage || "Sell all failed")
              : `Sell all ${final.status}`,
            final.status === "error"
          );
        }
        await refreshAll();
      }
    } catch (err) {
      showToast(err.message, true);
    }
  });
}

async function pauseBuying() {
  const button = el("pauseBtn");
  await withButtonDisabled(button, async () => {
    try {
      const response = await apiSend("POST", "/api/trades/pause-buying");
      showToast(response.message || "Pause requested");
      await refreshAll();
    } catch (err) {
      showToast(err.message, true);
    }
  });
}

async function resumeBuying() {
  const button = el("resumeBtn");
  await withButtonDisabled(button, async () => {
    try {
      const response = await apiSend("POST", "/api/trades/resume-buying");
      showToast(response.message || "Resume requested");
      await refreshAll();
    } catch (err) {
      showToast(err.message, true);
    }
  });
}

async function cancelCommand(id, button) {
  await withButtonDisabled(button, async () => {
    try {
      const response = await apiSend("DELETE", `/api/trades/commands/${encodeURIComponent(id)}`);
      showToast(response.message || "Cancelled");
      await refreshAll();
    } catch (err) {
      showToast(err.message, true);
    }
  });
}

// ---- Wiring ----

el("refreshBtn").addEventListener("click", refreshAll);
el("sellAllBtn").addEventListener("click", sellAll);
el("pauseBtn").addEventListener("click", pauseBuying);
el("resumeBtn").addEventListener("click", resumeBuying);

el("settingsBtn").addEventListener("click", () => {
  el("apiKeyInput").value = getApiKey();
  el("settingsPanel").classList.toggle("hidden");
});
el("saveKeyBtn").addEventListener("click", () => {
  localStorage.setItem(API_KEY_STORAGE, el("apiKeyInput").value);
  el("settingsPanel").classList.add("hidden");
  showToast("API key saved");
});

refreshAll();
setInterval(refreshAll, REFRESH_INTERVAL_MS);
