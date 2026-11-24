(() => {
  const form = document.querySelector("#survey-form");
  const status = document.querySelector("#submit-status");
  const button = document.querySelector("#submit-button");
  const example = document.querySelector("#example");
  const exampleCount = document.querySelector("#example-count");
  const noneOption = document.querySelector('input[name="difficulties"][value="sem-dificuldades"]');
  const otherDifficulties = [...document.querySelectorAll('input[name="difficulties"]')].filter((input) => input !== noneOption);

  example.addEventListener("input", () => {
    exampleCount.textContent = String(example.value.length);
  });

  noneOption.addEventListener("change", () => {
    if (noneOption.checked) otherDifficulties.forEach((input) => { input.checked = false; });
  });
  otherDifficulties.forEach((input) => input.addEventListener("change", () => {
    if (input.checked) noneOption.checked = false;
  }));

  function selected(name) {
    return [...form.querySelectorAll(`input[name="${name}"]:checked`)].map((input) => input.value);
  }

  function setError(group, message) {
    const block = document.querySelector(`[data-required-group="${group}"]`);
    const error = document.querySelector(`[data-error-for="${group}"]`);
    block.classList.toggle("has-error", Boolean(message));
    error.textContent = message || "";
  }

  function validate() {
    let valid = true;
    for (const [key, label] of [["platforms", "Escolha ao menos uma opção."], ["contentTypes", "Escolha ao menos um conteúdo."], ["difficulties", "Escolha uma opção, mesmo que não tenha encontrado dificuldades."]]) {
      const okay = selected(key).length > 0;
      setError(key, okay ? "" : label);
      valid = okay && valid;
    }
    const frequency = form.querySelector('input[name="frequency"]:checked');
    setError("frequency", frequency ? "" : "Selecione uma frequência.");
    return valid && Boolean(frequency);
  }

  form.addEventListener("change", (event) => {
    const group = event.target.name;
    if (["platforms", "contentTypes", "difficulties", "frequency"].includes(group)) setError(group, "");
  });

  form.addEventListener("submit", async (event) => {
    event.preventDefault();
    status.className = "submit-status";
    status.textContent = "";
    if (!validate()) {
      document.querySelector(".has-error")?.scrollIntoView({ behavior: "smooth", block: "center" });
      return;
    }

    const apiBase = window.ESCUTA_API_BASE_URL;
    if (!apiBase || apiBase.includes("CONFIGURE-A-API-AQUI")) {
      status.classList.add("error");
      status.textContent = "A página ainda está sendo configurada. Tente novamente mais tarde.";
      return;
    }

    const payload = {
      platforms: selected("platforms"),
      otherPlatform: form.elements.otherPlatform.value.trim(),
      contentTypes: selected("contentTypes"),
      frequency: form.querySelector('input[name="frequency"]:checked').value,
      difficulties: selected("difficulties"),
      example: form.elements.example.value.trim(),
      worksWell: form.elements.worksWell.value.trim(),
      suggestion: form.elements.suggestion.value.trim(),
      website: form.elements.website.value.trim()
    };

    button.disabled = true;
    button.querySelector("span").textContent = "Enviando…";
    try {
      const response = await fetch(`${apiBase.replace(/\/$/, "")}/api/v1/responses`, {
        method: "POST",
        headers: { "Content-Type": "application/json" },
        body: JSON.stringify(payload)
      });
      if (!response.ok) throw new Error(response.status === 429 ? "Muitas tentativas em sequência. Aguarde um pouco e tente novamente." : "Não foi possível registrar sua resposta agora.");
      document.querySelector(".form-intro").hidden = true;
      form.innerHTML = `<div class="success-panel"><div class="success-seal" aria-hidden="true">✦</div><p class="eyebrow">Resposta recebida</p><h2>Obrigado por compartilhar sua experiência.</h2><p>Sua contribuição vai nos ajudar a entender melhor como as pessoas acompanham a liturgia no dia a dia.</p></div>`;
      form.scrollIntoView({ behavior: "smooth", block: "center" });
    } catch (error) {
      status.classList.add("error");
      status.textContent = error instanceof Error ? error.message : "O envio não foi concluído. Tente novamente.";
      button.disabled = false;
      button.querySelector("span").textContent = "Enviar minha experiência";
    }
  });
})();
