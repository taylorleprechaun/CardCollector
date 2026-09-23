const MATCH_GENERIC_ERROR = 'Something went wrong saving your change. Please try again.';
const MATCH_MISSING_ERROR = 'That round or event no longer exists. Refresh the page and try again.';

// Mirrors MatchRules.SuggestResult on the server; the stored result may still be overridden by hand.
function suggestMatchResult(won, lost, tied) {
    const halfTies = tied / 2;
    if (won > lost + halfTies) return 'Win';
    return lost > won + halfTies ? 'Loss' : 'Tie';
}

function matchField(form, name) {
    return form.querySelector(`[data-match-field="${name}"]`);
}

function readMatchScores(form) {
    return {
        won: Number(matchField(form, 'won').value) || 0,
        lost: Number(matchField(form, 'lost').value) || 0,
        tied: Number(matchField(form, 'tied').value) || 0
    };
}

function writeMatchScores(form, won, lost, tied) {
    matchField(form, 'won').value = won;
    matchField(form, 'lost').value = lost;
    matchField(form, 'tied').value = tied;
}

function setMatchResult(form, result) {
    form.querySelectorAll('[data-match-result]').forEach((radio) => {
        radio.checked = radio.value === result;
    });
}

function getMatchResult(form) {
    return form.querySelector('[data-match-result]:checked')?.value ?? '';
}

function setMatchDice(form, value) {
    form.querySelectorAll('[data-match-dice]').forEach((radio) => {
        radio.checked = radio.value === value;
    });
}

function syncScorePresets(form) {
    const { won, lost, tied } = readMatchScores(form);
    form.querySelectorAll('[data-score-preset]').forEach((radio) => {
        radio.checked = Number(radio.dataset.won) === won
            && Number(radio.dataset.lost) === lost
            && Number(radio.dataset.tied) === tied;
    });
}

function updateResultHint(form, suggestion) {
    const hint = form.querySelector('[data-result-hint]');
    if (!hint) return;

    hint.hidden = form.dataset.resultOverridden !== 'true';
    hint.querySelector('[data-result-suggestion]').textContent = suggestion;
}

// The result follows the score until the user picks something else; after that it is left alone.
function refreshMatchResult(form) {
    const { won, lost, tied } = readMatchScores(form);
    const suggestion = suggestMatchResult(won, lost, tied);

    if (form.dataset.resultOverridden !== 'true') setMatchResult(form, suggestion);
    updateResultHint(form, suggestion);
}

function onMatchScoresChanged(form) {
    syncScorePresets(form);
    refreshMatchResult(form);
}

function onMatchResultPicked(form) {
    const { won, lost, tied } = readMatchScores(form);
    form.dataset.resultOverridden = String(getMatchResult(form) !== suggestMatchResult(won, lost, tied));
    refreshMatchResult(form);
}

function resetMatchResultOverride(form) {
    form.dataset.resultOverridden = 'false';
    refreshMatchResult(form);
}

function applyMatchPreset(form, radio) {
    writeMatchScores(form, radio.dataset.won, radio.dataset.lost, radio.dataset.tied);
    refreshMatchResult(form);
}

// The number boxes stay out of the way behind "Custom score" unless a round's score isn't one of the quick fills.
function setCustomScoreVisible(form, visible) {
    const section = form.querySelector('[data-custom-score]');
    const toggle = form.querySelector('[data-custom-toggle]');

    if (section) section.hidden = !visible;
    if (toggle) toggle.setAttribute('aria-expanded', String(visible));
}

function toggleCustomScore(form) {
    const section = form.querySelector('[data-custom-score]');
    if (section) setCustomScoreVisible(form, section.hidden);
}

function setMatchByeVisibility(form, isBye) {
    const section = form.querySelector('[data-bye-hide]');
    if (section) section.hidden = isBye;
}

// A bye has no games, dice roll or chosen result: the server forces a win with no games.
function onMatchByeToggled(form, isBye) {
    setMatchByeVisibility(form, isBye);

    writeMatchScores(form, 0, 0, 0);
    syncScorePresets(form);
    setCustomScoreVisible(form, false);
    setMatchDice(form, '');
    form.dataset.resultOverridden = 'false';
    if (isBye) {
        setMatchResult(form, 'Win');
        updateResultHint(form, 'Win');
    } else {
        refreshMatchResult(form);
    }
}

function clearMatchErrors(form) {
    const box = form.querySelector('[data-match-errors]');
    if (!box) return;

    box.querySelector('ul').replaceChildren();
    box.hidden = true;
}

function showMatchErrors(form, errors) {
    const box = form.querySelector('[data-match-errors]');
    if (!box) return;

    const list = box.querySelector('ul');
    list.replaceChildren();
    errors.forEach((message) => {
        const item = document.createElement('li');
        item.textContent = message;
        list.appendChild(item);
    });
    box.hidden = false;
}

// After add, the form starts over for the next round with the cursor on Opponent: the fast path at a tournament.
function resetMatchForm(form, nextRound) {
    const round = matchField(form, 'round');
    round.value = nextRound || '';
    round.dataset.suggested = nextRound || '';

    matchField(form, 'id').value = '0';
    matchField(form, 'bye').checked = false;
    matchField(form, 'opponent').value = '';
    matchField(form, 'notes').value = '';
    onMatchByeToggled(form, false);
    clearMatchErrors(form);

    matchField(form, 'opponent').focus();
}

// Leaves the add form's round alone if the user has already typed a different one.
function refreshSuggestedRound(nextRound) {
    const round = document.querySelector('[data-match-form="add"] [data-match-field="round"]');
    if (!round) return;

    if (round.value === '' || round.value === round.dataset.suggested) round.value = nextRound || '';
    round.dataset.suggested = nextRound || '';
}

function populateMatchForm(form, data) {
    const isBye = data.bye === 'true';

    matchField(form, 'id').value = data.matchId || '0';
    matchField(form, 'round').value = data.round || '';
    matchField(form, 'bye').checked = isBye;
    matchField(form, 'opponent').value = data.opponent || '';
    matchField(form, 'notes').value = data.notes || '';
    writeMatchScores(form, data.gamesWon || 0, data.gamesLost || 0, data.gamesTied || 0);
    syncScorePresets(form);
    setCustomScoreVisible(form, !isBye && !form.querySelector('[data-score-preset]:checked'));
    setMatchDice(form, data.dice || '');
    setMatchByeVisibility(form, isBye);

    // A stored result that disagrees with the score was set by hand, so editing the score must not change it.
    const { won, lost, tied } = readMatchScores(form);
    const suggestion = suggestMatchResult(won, lost, tied);
    const result = data.result || suggestion;
    form.dataset.resultOverridden = String(!isBye && result !== suggestion);
    setMatchResult(form, result);
    updateResultHint(form, suggestion);
    clearMatchErrors(form);
}

function openEditMatchModal(row) {
    const modal = document.getElementById('editMatchModal');
    populateMatchForm(modal.querySelector('[data-match-form]'), row.dataset);
    new bootstrap.Modal(modal).show();
}

function openDeleteMatchModal(row) {
    const modal = document.getElementById('deleteMatchModal');
    const opponent = row.dataset.opponent || '';

    document.getElementById('deleteMatchID').value = row.dataset.matchId;
    document.getElementById('deleteMatchName').textContent = `Round ${row.dataset.round}${opponent ? ` vs ${opponent}` : ''}`;
    clearMatchErrors(modal.querySelector('[data-match-form]'));
    new bootstrap.Modal(modal).show();
}

function hideMatchModal(form) {
    const modal = form.closest('.modal');
    if (modal) bootstrap.Modal.getInstance(modal)?.hide();
}

// The server places a new round by its label, so it can land between existing rounds rather than at the end.
function insertMatchRow(list, result) {
    if (result.previousMatchId === null || result.previousMatchId === undefined) {
        list.insertAdjacentHTML('afterbegin', result.rowHtml);
        return;
    }

    const previous = document.getElementById(`match-row-${result.previousMatchId}`);
    if (previous) previous.insertAdjacentHTML('afterend', result.rowHtml);
    else list.insertAdjacentHTML('beforeend', result.rowHtml);
}

// Only the touched row is patched; every other row stays exactly as it was.
function applyMatchResult(form, kind, result) {
    const list = document.getElementById('matchList');
    const summary = document.getElementById('matchSummary');
    const hasRounds = result.roundCount > 0;

    if (kind === 'add') {
        insertMatchRow(list, result);
    } else if (kind === 'edit') {
        const row = document.getElementById(`match-row-${matchField(form, 'id').value}`);
        if (row) row.outerHTML = result.rowHtml;
    } else {
        document.getElementById(`match-row-${document.getElementById('deleteMatchID').value}`)?.remove();
    }

    summary.innerHTML = result.summaryHtml;
    summary.hidden = !hasRounds;
    list.hidden = !hasRounds;
    document.getElementById('matchEmpty').hidden = hasRounds;

    if (kind === 'add') {
        resetMatchForm(form, result.nextRound);
        return;
    }

    refreshSuggestedRound(result.nextRound);
    hideMatchModal(form);
}

async function submitMatchForm(form) {
    const kind = form.dataset.matchForm;
    const buttons = form.querySelectorAll('[type="submit"]');

    clearMatchErrors(form);
    buttons.forEach((button) => { button.disabled = true; });

    try {
        const response = await fetch(form.action, {
            method: 'POST',
            body: new FormData(form),
            headers: { 'X-Requested-With': 'XMLHttpRequest' }
        });

        if (response.status === 400) {
            showMatchErrors(form, (await response.json()).errors);
            return;
        }

        if (!response.ok) {
            showMatchErrors(form, [response.status === 404 ? MATCH_MISSING_ERROR : MATCH_GENERIC_ERROR]);
            return;
        }

        applyMatchResult(form, kind, await response.json());
    } catch (err) {
        console.error('Saving the round failed', err);
        showMatchErrors(form, [MATCH_GENERIC_ERROR]);
    } finally {
        buttons.forEach((button) => { button.disabled = false; });
    }
}

// One set of delegated listeners for the page, bound once no matter how many times this script is evaluated.
if (!document.ccMatchListenersBound) {
    document.ccMatchListenersBound = true;

    document.addEventListener('input', (event) => {
        const form = event.target.closest?.('[data-match-form]');
        if (form && event.target.matches('[data-match-field="won"], [data-match-field="lost"], [data-match-field="tied"]')) {
            onMatchScoresChanged(form);
        }
    });

    document.addEventListener('change', (event) => {
        const target = event.target;
        const form = target.closest?.('[data-match-form]');
        if (!form) return;

        if (target.matches('[data-score-preset]')) applyMatchPreset(form, target);
        else if (target.matches('[data-match-result]')) onMatchResultPicked(form);
        else if (target.matches('[data-match-field="bye"]')) onMatchByeToggled(form, target.checked);
    });

    document.addEventListener('click', (event) => {
        const customToggle = event.target.closest?.('[data-custom-toggle]');
        if (customToggle) {
            toggleCustomScore(customToggle.closest('[data-match-form]'));
            return;
        }

        const resetButton = event.target.closest?.('[data-result-reset]');
        if (resetButton) {
            resetMatchResultOverride(resetButton.closest('[data-match-form]'));
            return;
        }

        const actionButton = event.target.closest?.('[data-match-action]');
        const row = actionButton?.closest('[data-match-id]');
        if (!row) return;

        if (actionButton.dataset.matchAction === 'edit') openEditMatchModal(row);
        else openDeleteMatchModal(row);
    });

    // The Add round box starts collapsed; opening it puts the cursor on Opponent, ready to type.
    document.addEventListener('shown.bs.collapse', (event) => {
        if (event.target.id !== 'addRoundPanel') return;

        const form = event.target.querySelector('[data-match-form="add"]');
        if (form) matchField(form, 'opponent').focus();
    });

    document.addEventListener('submit', (event) => {
        const form = event.target;
        if (!(form instanceof HTMLFormElement) || !form.matches('[data-match-form]')) return;

        event.preventDefault();
        submitMatchForm(form);
    });
}
