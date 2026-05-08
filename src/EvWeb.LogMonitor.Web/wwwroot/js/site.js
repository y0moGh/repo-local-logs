document.addEventListener("DOMContentLoaded", () => {
    initializeDashboard(document);
});

let activeViewerRequestController = null;
let activeBatchRequestController = null;
let latestViewerRequestId = 0;

window.addEventListener("popstate", () => {
    const root = document.querySelector("[data-dashboard-root]");
    if (!(root instanceof HTMLElement)) {
        return;
    }

    void loadLogViewer(window.location.href, { pushState: false, preserveTextCursor: false, syncFormState: true });
});

function initializeDashboard(scope) {
    const root = scope.querySelector("[data-dashboard-root]");
    if (!(root instanceof HTMLElement)) {
        return;
    }

    wireDashboardForm(root);
    wireInfiniteLogScroll(root);
    focusTextInput(root);
}

function wireDashboardForm(root) {
    const form = root.querySelector("[data-dashboard-form]");
    if (!(form instanceof HTMLFormElement)) {
        return;
    }

    form.addEventListener("submit", (event) => {
        event.preventDefault();
        void loadLogViewer(buildFormUrl(root, form));
    });

    const inputElements = form.querySelectorAll("[data-dashboard-trigger='input']");
    for (const element of inputElements) {
        if (element instanceof HTMLInputElement) {
            const debouncedHandler = debounce(() => {
                void loadLogViewer(buildFormUrl(root, form));
            }, 250);

            element.addEventListener("input", debouncedHandler);
        }
    }

    const changeElements = form.querySelectorAll("[data-dashboard-trigger='change']");
    for (const element of changeElements) {
        element.addEventListener("change", () => {
            const syncFormState = element instanceof HTMLSelectElement
                && (element.name === "folder" || element.name === "source" || element.name === "file");

            void loadLogViewer(buildFormUrl(root, form), { syncFormState });
        });
    }

    const links = root.querySelectorAll("[data-dashboard-link]");
    for (const link of links) {
        if (link instanceof HTMLAnchorElement) {
            link.addEventListener("click", (event) => {
                event.preventDefault();

                const sourceName = link.dataset.sourceName;
                if (typeof sourceName === "string" && sourceName.length > 0) {
                    const sourceInput = form.querySelector("input[name='source']");
                    if (sourceInput instanceof HTMLInputElement) {
                        sourceInput.value = sourceName;
                    }

                    syncSelectedSource(root, sourceName);
                }

                void loadLogViewer(buildFormUrl(root, form), { syncFormState: true });
            });
        }
    }
}

function wireInfiniteLogScroll(root) {
    const scrollContainer = root.querySelector("[data-log-lines-scroll]");
    if (!(scrollContainer instanceof HTMLElement)) {
        return;
    }

    if (scrollContainer.dataset.scrollBound === "true") {
        updateInfiniteScrollState(scrollContainer);
        void ensureScrollableContent(root, scrollContainer);
        return;
    }

    scrollContainer.dataset.scrollBound = "true";
    updateInfiniteScrollState(scrollContainer);

    scrollContainer.addEventListener("scroll", () => {
        const nearBottom = scrollContainer.scrollTop + scrollContainer.clientHeight >= scrollContainer.scrollHeight - 80;
        if (nearBottom) {
            void loadMoreLogLines(root, scrollContainer);
        }
    });

    void ensureScrollableContent(root, scrollContainer);
}

async function loadLogViewer(url, options = {}) {
    const root = document.querySelector("[data-dashboard-root]");
    const viewerContainer = document.querySelector("[data-log-viewer-container]");
    if (!(root instanceof HTMLElement) || !(viewerContainer instanceof HTMLElement)) {
        return;
    }

    if (activeViewerRequestController instanceof AbortController) {
        activeViewerRequestController.abort();
    }

    if (activeBatchRequestController instanceof AbortController) {
        activeBatchRequestController.abort();
    }

    activeViewerRequestController = new AbortController();
    const requestId = ++latestViewerRequestId;

    setLoadingState(viewerContainer, true);

    try {
        const pageUrl = root.dataset.pageUrl ?? "/Logs";
        const dashboardUrl = root.dataset.dashboardUrl ?? pageUrl;
        const requestUrl = translateUrl(url, pageUrl, dashboardUrl);

        const response = await fetch(requestUrl, {
            headers: {
                "X-Requested-With": "XMLHttpRequest"
            },
            signal: activeViewerRequestController.signal
        });

        if (!response.ok) {
            window.location.href = url;
            return;
        }

        const html = await response.text();
        if (requestId !== latestViewerRequestId) {
            return;
        }

        const nextDashboard = createDashboardContentElement(html);
        if (!(nextDashboard instanceof HTMLElement)) {
            window.location.href = url;
            return;
        }

        if (options.syncFormState === true) {
            syncDashboardState(root, nextDashboard);
        }

        const nextViewer = nextDashboard.querySelector("[data-log-viewer]");
        if (!(nextViewer instanceof HTMLElement)) {
            window.location.href = url;
            return;
        }

        const currentSignature = getViewerSignature(viewerContainer);
        const nextSignature = nextViewer.dataset.contentSignature ?? "";

        if (options.pushState !== false) {
            window.history.replaceState({}, "", normalizeHistoryUrl(url, pageUrl));
        }

        syncSelectedSourceFromUrl(root, url);

        if (currentSignature !== nextSignature) {
            viewerContainer.replaceChildren(nextViewer);
        } else {
            syncViewerState(viewerContainer, nextViewer);
        }

        wireInfiniteLogScroll(root);
        focusTextInput(root, options.preserveTextCursor !== false);
    } catch (error) {
        if (error instanceof DOMException && error.name === "AbortError") {
            return;
        }

        window.location.href = url;
    } finally {
        if (requestId === latestViewerRequestId) {
            setLoadingState(viewerContainer, false);
        }
    }
}

async function loadMoreLogLines(root, scrollContainer) {
    if (!(scrollContainer instanceof HTMLElement)) {
        return;
    }

    const hasMore = scrollContainer.dataset.hasMore === "true";
    const isLoading = scrollContainer.dataset.loading === "true";
    if (!hasMore || isLoading) {
        return;
    }

    const list = scrollContainer.querySelector("[data-log-lines-list]");
    if (!(list instanceof HTMLElement)) {
        return;
    }

    if (activeBatchRequestController instanceof AbortController) {
        activeBatchRequestController.abort();
    }

    activeBatchRequestController = new AbortController();
    scrollContainer.dataset.loading = "true";
    updateInfiniteScrollState(scrollContainer);

    try {
        const requestUrl = buildBatchUrl(scrollContainer);
        const response = await fetch(requestUrl, {
            headers: {
                "X-Requested-With": "XMLHttpRequest"
            },
            signal: activeBatchRequestController.signal
        });

        if (!response.ok) {
            return;
        }

        const html = await response.text();
        const batch = createBatchElement(html);
        if (!(batch instanceof HTMLElement)) {
            return;
        }

        const rows = Array.from(batch.children).filter((child) => child instanceof HTMLElement);
        list.append(...rows);
        applyBatchMetadata(scrollContainer, batch);
        updateViewerSummary(root, scrollContainer);
        await ensureScrollableContent(root, scrollContainer);
    } catch (error) {
        if (!(error instanceof DOMException && error.name === "AbortError")) {
            scrollContainer.dataset.hasMore = "false";
        }
    } finally {
        scrollContainer.dataset.loading = "false";
        updateInfiniteScrollState(scrollContainer);
    }
}

function syncViewerState(viewerContainer, nextViewer) {
    const currentScroll = viewerContainer.querySelector("[data-log-lines-scroll]");
    const nextScroll = nextViewer.querySelector("[data-log-lines-scroll]");

    if (!(currentScroll instanceof HTMLElement) || !(nextScroll instanceof HTMLElement)) {
        return;
    }

    currentScroll.dataset.source = nextScroll.dataset.source ?? "";
    currentScroll.dataset.folder = nextScroll.dataset.folder ?? "";
    currentScroll.dataset.file = nextScroll.dataset.file ?? "";
    currentScroll.dataset.text = nextScroll.dataset.text ?? "";
    currentScroll.dataset.dateFrom = nextScroll.dataset.dateFrom ?? "";
    currentScroll.dataset.dateTo = nextScroll.dataset.dateTo ?? "";
    currentScroll.dataset.association = nextScroll.dataset.association ?? "";
    currentScroll.dataset.logType = nextScroll.dataset.logType ?? "";
    currentScroll.dataset.maxLines = nextScroll.dataset.maxLines ?? "";
    currentScroll.dataset.nextOffset = nextScroll.dataset.nextOffset ?? "";
    currentScroll.dataset.hasMore = nextScroll.dataset.hasMore ?? "false";
    currentScroll.dataset.loadedCount = nextScroll.dataset.loadedCount ?? "0";
    currentScroll.dataset.availableCount = nextScroll.dataset.availableCount ?? "0";
    updateInfiniteScrollState(currentScroll);
    updateViewerSummary(document, currentScroll);
}

function updateViewerSummary(root, scrollContainer) {
    const viewer = root.querySelector("[data-log-viewer]");
    if (!(viewer instanceof HTMLElement)) {
        return;
    }

    const summary = viewer.querySelector("[data-log-viewer-summary]");
    if (!(summary instanceof HTMLElement)) {
        return;
    }

    const totalLines = viewer.dataset.totalLines ?? "";
    const loadedCount = scrollContainer.dataset.loadedCount ?? "0";
    const availableCount = scrollContainer.dataset.availableCount ?? "0";
    summary.textContent = `Total archivo: ${totalLines} lineas | Cargadas: ${loadedCount} de ${availableCount}`;
}

function updateInfiniteScrollState(scrollContainer) {
    const loadingElement = scrollContainer.querySelector("[data-log-lines-loading]");
    const endElement = scrollContainer.querySelector("[data-log-lines-end]");

    const isLoading = scrollContainer.dataset.loading === "true";
    const hasMore = scrollContainer.dataset.hasMore === "true";

    if (loadingElement instanceof HTMLElement) {
        loadingElement.hidden = !isLoading;
    }

    if (endElement instanceof HTMLElement) {
        endElement.hidden = hasMore || isLoading;
    }
}

async function ensureScrollableContent(root, scrollContainer) {
    while (scrollContainer.dataset.hasMore === "true"
        && scrollContainer.dataset.loading !== "true"
        && scrollContainer.scrollHeight <= scrollContainer.clientHeight + 24) {
        await loadMoreLogLines(root, scrollContainer);
    }
}

function createViewerElement(html) {
    const template = document.createElement("template");
    template.innerHTML = html.trim();
    return template.content.querySelector("[data-log-viewer]");
}

function createDashboardContentElement(html) {
    const template = document.createElement("template");
    template.innerHTML = html.trim();
    return template.content.querySelector("[data-dashboard-content]");
}

function createBatchElement(html) {
    const template = document.createElement("template");
    template.innerHTML = html.trim();
    return template.content.querySelector("[data-log-line-batch]");
}

function getViewerSignature(viewerContainer) {
    const viewer = viewerContainer.querySelector("[data-log-viewer]");
    return viewer instanceof HTMLElement ? viewer.dataset.contentSignature ?? "" : "";
}

function applyBatchMetadata(scrollContainer, batch) {
    scrollContainer.dataset.nextOffset = batch.dataset.nextOffset ?? scrollContainer.dataset.nextOffset ?? "0";
    scrollContainer.dataset.hasMore = batch.dataset.hasMore ?? "false";
    scrollContainer.dataset.loadedCount = batch.dataset.loadedCount ?? scrollContainer.dataset.loadedCount ?? "0";
    scrollContainer.dataset.availableCount = batch.dataset.availableCount ?? scrollContainer.dataset.availableCount ?? "0";
}

function buildFormUrl(root, form) {
    const pageUrl = root.dataset.pageUrl ?? form.action;
    const url = new URL(pageUrl, window.location.origin);
    const formData = new FormData(form);

    for (const [key, value] of formData.entries()) {
        const normalized = String(value).trim();
        if (normalized.length > 0) {
            url.searchParams.set(key, normalized);
        }
    }

    return url.toString();
}

function buildBatchUrl(scrollContainer) {
    const batchUrl = scrollContainer.dataset.batchUrl ?? "/Logs/LinesBatch";
    const url = new URL(batchUrl, window.location.origin);
    const params = {
        source: scrollContainer.dataset.source ?? "",
        folder: scrollContainer.dataset.folder ?? "",
        file: scrollContainer.dataset.file ?? "",
        text: scrollContainer.dataset.text ?? "",
        dateFrom: scrollContainer.dataset.dateFrom ?? "",
        dateTo: scrollContainer.dataset.dateTo ?? "",
        association: scrollContainer.dataset.association ?? "",
        logType: scrollContainer.dataset.logType ?? "",
        maxLines: scrollContainer.dataset.maxLines ?? "500",
        offset: scrollContainer.dataset.nextOffset ?? "0"
    };

    for (const [key, value] of Object.entries(params)) {
        if (value.length > 0) {
            url.searchParams.set(key, value);
        }
    }

    return url.toString();
}

function translateUrl(url, pageUrl, dashboardUrl) {
    const parsed = new URL(url, window.location.origin);
    const target = new URL(dashboardUrl, window.location.origin);
    target.search = parsed.search;
    return target.toString();
}

function normalizeHistoryUrl(url, pageUrl) {
    const parsed = new URL(url, window.location.origin);
    const target = new URL(pageUrl, window.location.origin);
    target.search = parsed.search;
    return target.toString();
}

function syncDashboardState(root, nextDashboard) {
    replaceSection(root, ".hero-panel", nextDashboard.querySelector(".hero-panel"));
    replaceSection(root, ".source-tabs", nextDashboard.querySelector(".source-tabs"));
    replaceSection(root, ".filter-panel", nextDashboard.querySelector(".filter-panel"));
    wireDashboardForm(root);
}

function replaceSection(root, selector, nextSection) {
    const currentSection = root.querySelector(selector);
    if (!(currentSection instanceof HTMLElement) || !(nextSection instanceof HTMLElement)) {
        return;
    }

    currentSection.replaceWith(nextSection);
}

function setLoadingState(viewerContainer, isLoading) {
    viewerContainer.classList.toggle("is-loading", isLoading);
}

function syncSelectedSourceFromUrl(root, url) {
    const parsed = new URL(url, window.location.origin);
    const source = parsed.searchParams.get("source");
    if (typeof source === "string" && source.length > 0) {
        syncSelectedSource(root, source);
    }
}

function syncSelectedSource(root, sourceName) {
    const sourceInput = root.querySelector("input[name='source']");
    if (sourceInput instanceof HTMLInputElement) {
        sourceInput.value = sourceName;
    }

    const title = root.querySelector(".filter-panel .panel-header h3");
    if (title instanceof HTMLElement) {
        title.textContent = sourceName;
    }

    const tabs = root.querySelectorAll("[data-dashboard-link][data-source-name]");
    for (const tab of tabs) {
        if (tab instanceof HTMLElement) {
            tab.classList.toggle("active", tab.dataset.sourceName === sourceName);
        }
    }
}

function focusTextInput(root, preserveCursor = true) {
    const textInput = root.querySelector("input[name='text']");
    if (!(textInput instanceof HTMLInputElement)) {
        return;
    }

    const currentStart = textInput.selectionStart ?? textInput.value.length;
    const currentEnd = textInput.selectionEnd ?? textInput.value.length;
    textInput.focus({ preventScroll: true });

    if (preserveCursor) {
        textInput.setSelectionRange(currentStart, currentEnd);
    }
}

function debounce(callback, waitMs) {
    let timeoutId = 0;

    return () => {
        window.clearTimeout(timeoutId);
        timeoutId = window.setTimeout(callback, waitMs);
    };
}
