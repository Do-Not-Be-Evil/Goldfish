// In-browser help for Pages/Changes/Edit: add and remove rule rows without a round trip, and show each
// rule's multiplier ratio and whether it needs a reason while the analyst types. The server repeats every
// check on save (RuleSetValidator, RuleSetDiff), so this script only gives earlier feedback.

interface BaseRule {
    name: string;
    multiplier: number;
}

interface EditorData {
    band: { min: number; max: number };
    baseRules: BaseRule[];
}

const form = document.querySelector<HTMLFormElement>("form[data-rule-editor]");
if (form) {
    setUp(form);
}

function setUp(editor: HTMLFormElement): void {
    const dataElement = document.getElementById("rule-editor-data");
    const data: EditorData = JSON.parse(dataElement?.textContent ?? "{}");
    const rows = editor.querySelector<HTMLTableSectionElement>("[data-rule-rows]")!;
    const template = editor.querySelector<HTMLTemplateElement>("template[data-row-template]")!;

    // Rule names match without regard to case, like the database collation and RuleSetDiff.
    const baseByName = new Map<string, number>(
        (data.baseRules ?? []).map((r) => [r.name.trim().toLowerCase(), r.multiplier]));

    editor.addEventListener("click", (event) => {
        const target = event.target as HTMLElement;

        if (target.closest("[data-add-row]")) {
            event.preventDefault();
            addRow();
            return;
        }

        const remove = target.closest<HTMLButtonElement>("[data-remove-row]");
        if (remove) {
            event.preventDefault();
            removeRow(remove.closest<HTMLTableRowElement>("tr[data-rule-row]")!);
        }
    });

    editor.addEventListener("input", (event) => {
        const row = (event.target as HTMLElement).closest<HTMLTableRowElement>("tr[data-rule-row]");
        if (row) {
            updateRatio(row);
        }
    });

    for (const row of ruleRows()) {
        updateRatio(row);
    }

    function ruleRows(): HTMLTableRowElement[] {
        return Array.from(rows.querySelectorAll<HTMLTableRowElement>("tr[data-rule-row]"));
    }

    function addRow(): void {
        const index = ruleRows().length;
        const fragment = template.content.cloneNode(true) as DocumentFragment;
        const row = fragment.querySelector<HTMLTableRowElement>("tr")!;
        renumber(row, index);

        const priorities = ruleRows()
            .map((r) => Number(field(r, "Priority")?.value))
            .filter((p) => Number.isFinite(p) && p > 0);
        const priority = field(row, "Priority");
        if (priority) {
            priority.value = String((priorities.length ? Math.max(...priorities) : 0) + 10);
        }

        rows.appendChild(row);
        updateRatio(row);
        field(row, "Name")?.focus();
    }

    function removeRow(row: HTMLTableRowElement): void {
        // Server-rendered problem rows describe the old numbering, so drop them all.
        rows.querySelectorAll("tr[data-problem-for]").forEach((p) => p.remove());
        row.remove();
        ruleRows().forEach((r, i) => renumber(r, i));
    }

    function renumber(row: HTMLTableRowElement, index: number): void {
        row.querySelectorAll<HTMLInputElement | HTMLSelectElement>("input, select").forEach((input) => {
            input.name = input.name.replace(/Rules\[(\d+|__i__)\]/, `Rules[${index}]`);
            if (input.id) {
                input.id = input.id.replace(/Rules_(\d+|__i__)__/, `Rules_${index}__`);
            }
        });

        const remove = row.querySelector<HTMLButtonElement>("[data-remove-row]");
        if (remove) {
            remove.value = `remove:${index}`;
        }

        const number = row.querySelector("[data-row-number]");
        if (number) {
            number.textContent = String(index + 1);
        }
    }

    function updateRatio(row: HTMLTableRowElement): void {
        const name = field(row, "Name")?.value.trim().toLowerCase() ?? "";
        const multiplier = Number(field(row, "Multiplier")?.value);
        const reason = field(row, "Reason");
        const cell = row.querySelector<HTMLElement>("[data-ratio]");
        if (!cell || !reason) {
            return;
        }

        let needsReason = false;
        cell.classList.remove("out-of-band");

        if (!name) {
            cell.textContent = "";
        } else if (!baseByName.has(name)) {
            cell.textContent = "new";
            needsReason = true;
        } else if (Number.isFinite(multiplier) && multiplier > 0) {
            // Rounded to 4 places, as the publish dry run and rulesadmin.ApproveAndPublish do.
            const ratio = Math.round((multiplier / baseByName.get(name)!) * 10000) / 10000;
            cell.textContent = ratio.toFixed(4).replace(/\.?0+$/, "");
            needsReason = ratio < data.band.min || ratio > data.band.max;
            cell.classList.toggle("out-of-band", needsReason);
        } else {
            cell.textContent = "";
        }

        reason.placeholder = needsReason ? "Reason required" : "";
        reason.classList.toggle("needs-reason", needsReason && reason.value.trim() === "");
    }

    function field(row: HTMLTableRowElement, property: string): HTMLInputElement | null {
        return row.querySelector<HTMLInputElement>(`[name$="].${property}"]`);
    }
}
