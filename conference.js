(() => {
  const logo = document.querySelector(".brand");
  const main = document.querySelector("main");
  const apiBase = () => (window.ESCUTA_API_BASE_URL || "").replace(/\/$/, "");
  let clicks = 0, timer, token = sessionStorage.getItem("conferenceToken") || "", items = [];

  logo.addEventListener("click", event => {
    event.preventDefault();
    clicks++;
    clearTimeout(timer);
    timer = setTimeout(() => { clicks = 0; }, 3500);
    if (clicks === 7) { clicks = 0; openLogin(); }
  });

  function openLogin() {
    const dialog = document.createElement("dialog");
    dialog.className = "conference-dialog";
    dialog.innerHTML = '<form class="conference-card" id="conference-login"><button class="dialog-close" type="button" aria-label="Fechar">�</button><p class="eyebrow">�rea de confer�ncia</p><h2>Acessar respostas</h2><label class="conference-label">Senha<input class="conference-input" name="password" type="password" required autocomplete="current-password"></label><p class="conference-status" role="status"></p><button class="conference-primary">Entrar</button></form>';
    document.body.append(dialog);
    dialog.showModal();
    dialog.querySelector(".dialog-close").onclick = () => dialog.close();
    dialog.addEventListener("close", () => dialog.remove());
    dialog.querySelector("form").onsubmit = async event => {
      event.preventDefault();
      const form = event.currentTarget, status = form.querySelector(".conference-status");
      status.textContent = "Conferindo...";
      try {
        const response = await fetch(apiBase() + "/api/v1/conference/login", { method: "POST", headers: { "Content-Type": "application/json" }, body: JSON.stringify({ password: form.elements.password.value }) });
        if (!response.ok) throw Error(response.status === 429 ? "Muitas tentativas. Aguarde alguns minutos." : "Senha incorreta ou servi�o indispon�vel.");
        token = (await response.json()).token;
        sessionStorage.setItem("conferenceToken", token);
        dialog.close();
        await showDashboard();
      } catch (error) { status.textContent = error.message; }
    };
  }

  async function api(path, options = {}) {
    const response = await fetch(apiBase() + path, { ...options, headers: { "Content-Type": "application/json", Authorization: "Bearer " + token, ...(options.headers || {}) } });
    if (response.status === 401) { token = ""; sessionStorage.removeItem("conferenceToken"); throw Error("Sess�o encerrada. Entre novamente pela logo."); }
    if (!response.ok) throw Error("N�o foi poss�vel concluir a opera��o.");
    return response.status === 204 ? null : response.json();
  }

  const labels = {
    platforms: { iliturgia: "iLiturgia", "outro-app": "Outro aplicativo", site: "Site", livro: "Livro impresso", youtube: "V�deos/transmiss�es", "nao-uso": "N�o usa atualmente" },
    content: { "liturgia-diaria": "Liturgia di�ria", laudes: "Laudes", tercia: "T�rcia", sexta: "Sexta", nona: "Nona", vesperas: "V�speras", completas: "Completas", "oficio-leituras": "Of�cio das Leituras", "acompanho-outro": "Outro momento" },
    frequency: { diariamente: "Todos ou quase todos os dias", "algumas-vezes-semana": "Algumas vezes por semana", semanalmente: "Uma vez por semana", ocasionalmente: "De vez em quando", "estou-comecando": "Est� come�ando agora" },
    difficulties: { "achar-conteudo": "Encontrar o conte�do do dia", "entender-ordem": "Entender a ordem da Hora", calendario: "Calend�rio/celebra��o", navegacao: "Navega��o entre partes", "leitura-celular": "Leitura no celular", offline: "Uso sem internet/�udio", "conteudo-incompleto": "Conte�do incompleto", "outra-dificuldade": "Outra dificuldade", "sem-dificuldades": "Sem dificuldades importantes" }
  };
  function decode(json, map) { try { return JSON.parse(json).map(value => map[value] || value).join(", ") || "-"; } catch { return "-"; } }
  function addField(card, title, value) {
    const row = document.createElement("div"), heading = document.createElement("strong"), text = document.createElement("p");
    row.className = "conference-answer"; heading.textContent = title; text.textContent = value || "-"; row.append(heading, text); card.append(row);
  }

  async function showDashboard() {
    document.querySelector("#conference-dashboard")?.remove();
    const panel = document.createElement("section");
    panel.id = "conference-dashboard";
    panel.className = "conference-dashboard";
    panel.innerHTML = '<div class="conference-heading"><div><p class="eyebrow">�rea de confer�ncia</p><h2>Respostas recebidas</h2></div><button class="conference-secondary" id="conference-logout">Sair</button></div><div class="conference-toolbar"><input class="conference-input" id="conference-search" placeholder="Buscar nas respostas..."><span id="conference-count"></span></div><p class="conference-status" id="conference-status" role="status"></p><div class="conference-list" id="conference-list"></div>';
    main.querySelector(".hero").hidden = true; main.querySelector(".form-wrap").hidden = true; main.querySelector(".closing-note").hidden = true; main.append(panel);
    panel.querySelector("#conference-logout").onclick = async () => {
      try { await api("/api/v1/conference/logout", { method: "POST" }); } catch {}
      token = ""; sessionStorage.removeItem("conferenceToken"); panel.remove();
      main.querySelector(".hero").hidden = false; main.querySelector(".form-wrap").hidden = false; main.querySelector(".closing-note").hidden = false;
    };
    panel.querySelector("#conference-search").oninput = render;
    try { items = await api("/api/v1/conference/responses"); render(); }
    catch (error) { panel.querySelector("#conference-status").textContent = error.message; }
  }

  function render() {
    const panel = document.querySelector("#conference-dashboard");
    if (!panel) return;
    const search = panel.querySelector("#conference-search").value.toLocaleLowerCase("pt-BR");
    const list = panel.querySelector("#conference-list");
    const filtered = items.filter(item => JSON.stringify(item).toLocaleLowerCase("pt-BR").includes(search));
    panel.querySelector("#conference-count").textContent = filtered.length + " de " + items.length + " respostas";
    list.replaceChildren();
    if (!filtered.length) { const empty = document.createElement("p"); empty.className = "conference-empty"; empty.textContent = items.length ? "Nenhuma resposta corresponde � busca." : "Ainda n�o h� respostas."; list.append(empty); return; }
    filtered.forEach(item => {
      const card = document.createElement("article"), header = document.createElement("div"), date = document.createElement("h3"), actions = document.createElement("div"), edit = document.createElement("button"), remove = document.createElement("button");
      card.className = "conference-card"; header.className = "conference-card-header"; date.textContent = new Date(item.submittedAtUtc + "Z").toLocaleString("pt-BR");
      actions.className = "conference-actions"; edit.className = "conference-secondary"; edit.textContent = "Editar"; edit.onclick = () => editResponse(item);
      remove.className = "conference-danger"; remove.textContent = "Excluir"; remove.onclick = () => deleteResponse(item);
      actions.append(edit, remove); header.append(date, actions); card.append(header);
      addField(card, "Meios usados", decode(item.platformsJson, labels.platforms) + (item.otherPlatform ? " � " + item.otherPlatform : ""));
      addField(card, "Conte�dos", decode(item.contentTypesJson, labels.content));
      addField(card, "Frequ�ncia", labels.frequency[item.frequency] || item.frequency);
      addField(card, "Dificuldades", decode(item.difficultiesJson, labels.difficulties));
      addField(card, "Exemplo", item.example); addField(card, "O que funciona bem", item.worksWell); addField(card, "Sugest�o", item.suggestion); list.append(card);
    });
  }

  async function editResponse(item) {
    const read = name => { try { return JSON.parse(item[name]).join(", "); } catch { return ""; } };
    const fields = [
      ["platformsJson", "Meios (valores separados por v�rgula)", read("platformsJson")],
      ["otherPlatform", "Outro aplicativo/site", item.otherPlatform || ""],
      ["contentTypesJson", "Conte�dos (valores separados por v�rgula)", read("contentTypesJson")],
      ["frequency", "Frequ�ncia (c�digo, ex.: diariamente)", item.frequency],
      ["difficultiesJson", "Dificuldades (valores separados por v�rgula)", read("difficultiesJson")],
      ["example", "Exemplo", item.example || ""], ["worksWell", "O que funciona bem", item.worksWell || ""], ["suggestion", "Sugest�o", item.suggestion || ""]
    ];
    const result = {};
    for (const [key, label, current] of fields) {
      const value = window.prompt(label, current);
      if (value === null) return;
      result[key] = value;
    }
    const split = value => value.split(",").map(part => part.trim()).filter(Boolean);
    const payload = {
      platforms: split(result.platformsJson), otherPlatform: result.otherPlatform,
      contentTypes: split(result.contentTypesJson), frequency: result.frequency.trim(),
      difficulties: split(result.difficultiesJson), example: result.example, worksWell: result.worksWell, suggestion: result.suggestion
    };
    try {
      await api("/api/v1/conference/responses/" + item.responseId, { method: "PUT", body: JSON.stringify(payload) });
      item.platformsJson = JSON.stringify(payload.platforms); item.otherPlatform = payload.otherPlatform;
      item.contentTypesJson = JSON.stringify(payload.contentTypes); item.frequency = payload.frequency;
      item.difficultiesJson = JSON.stringify(payload.difficulties); item.example = payload.example; item.worksWell = payload.worksWell; item.suggestion = payload.suggestion; render();
    } catch (error) { alert("N�o foi poss�vel salvar. Confira os dados e os c�digos das op��es.\n" + error.message); }
  }
  async function deleteResponse(item) {
    if (!confirm("Excluir esta resposta permanentemente?")) return;
    try { await api("/api/v1/conference/responses/" + item.responseId, { method: "DELETE" }); items = items.filter(value => value.responseId !== item.responseId); render(); }
    catch (error) { const status = document.querySelector("#conference-status"); if (status) status.textContent = error.message; }
  }
  if (token) showDashboard();
})();

