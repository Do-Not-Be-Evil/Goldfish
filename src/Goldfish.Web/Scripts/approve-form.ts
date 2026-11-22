// Pages/Changes/Details: keep "Approve and publish" disabled until the manager ticks the confirmation.
// The server refuses an unconfirmed approval anyway (RulesAdmin.ConfirmRequired).

for (const form of document.querySelectorAll<HTMLFormElement>("form[data-approve-form]")) {
    const confirm = form.querySelector<HTMLInputElement>("[data-approve-confirm]");
    const submit = form.querySelector<HTMLButtonElement>("[data-approve-submit]");
    if (!confirm || !submit) {
        continue;
    }

    const sync = (): void => {
        submit.disabled = !confirm.checked;
    };

    confirm.addEventListener("change", sync);
    sync();

    // Stop a double click from sending two approvals; the second would only report "already published".
    form.addEventListener("submit", () => {
        submit.disabled = true;
        submit.textContent = "Publishing…";
    });
}
