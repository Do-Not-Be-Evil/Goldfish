// Views/Policies/Create and Edit: summarise what the save will change, including the base-price change as a
// percentage, so a mistyped price (for example 10000 instead of 1000) stands out before it is saved.

const money = new Intl.NumberFormat(undefined, { minimumFractionDigits: 2, maximumFractionDigits: 2 });

for (const form of document.querySelectorAll<HTMLFormElement>("form[data-policy-form]")) {
    const summary = form.querySelector<HTMLElement>("#policy-change-summary");
    const type = form.querySelector<HTMLSelectElement>("select[name='PolicyType']");
    const coverage = form.querySelector<HTMLInputElement>("input[name='CoverageLimit']");
    const price = form.querySelector<HTMLInputElement>("input[name='BasePrice']");
    if (!summary || !type || !coverage || !price) {
        continue;
    }

    const update = (): void => {
        const changes: string[] = [];

        const originalType = type.dataset.original;
        if (originalType && type.value && type.value !== originalType) {
            changes.push(`type ${originalType} → ${type.value}`);
        }

        changes.push(...describe("coverage limit", coverage));

        const priceChanges = describe("base price", price);
        const before = Number(price.dataset.original);
        const after = Number(price.value);
        if (priceChanges.length && before > 0 && after > 0) {
            const percent = ((after - before) / before) * 100;
            priceChanges[0] += ` (${percent >= 0 ? "+" : ""}${percent.toFixed(1)}%)`;
            summary.classList.toggle("warning", Math.abs(percent) >= 25);
        } else {
            summary.classList.remove("warning");
        }
        changes.push(...priceChanges);

        summary.textContent = changes.length ? `Saving will change: ${changes.join("; ")}.` : "";
    };

    form.addEventListener("input", update);
    update();
}

function describe(label: string, input: HTMLInputElement): string[] {
    const original = input.dataset.original;
    if (!original || input.value === "") {
        return [];
    }

    const before = Number(original);
    const after = Number(input.value);
    if (!Number.isFinite(after) || after === before) {
        return [];
    }

    return [`${label} ${money.format(before)} → ${money.format(after)}`];
}
