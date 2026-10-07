import { css, html, customElement, state, repeat, nothing } from '@umbraco-cms/backoffice/external/lit';
import { UmbLitElement } from '@umbraco-cms/backoffice/lit-element';
import { UMB_NOTIFICATION_CONTEXT } from '@umbraco-cms/backoffice/notification';
import type { UUIPaginationEvent, UUISelectEvent } from '@umbraco-cms/backoffice/external/uui';
import { LinkHealthService } from './api/index.js';
import type {
  BrokenLinkModel,
  BrokenLinkOrder,
  LinkHealthSummaryResponse,
  LinkStatus,
  LinkVersionFilter,
} from './api/index.js';
import { documentHref, sharedStyles, statusColor, statusTerm, targetTemplate } from './link-health-shared.js';

const PAGE_SIZE = 25;
const POLL_MS = 3000;

const VERSIONS: Array<{ value: LinkVersionFilter; term: string }> = [
  { value: 'All', term: 'ddContentHealth_versionAll' },
  { value: 'Live', term: 'ddContentHealth_live' },
  { value: 'Draft', term: 'ddContentHealth_draft' },
];

@customElement('dd-link-health-dashboard')
export class DdLinkHealthDashboardElement extends UmbLitElement {
  @state() private _summary?: LinkHealthSummaryResponse;
  @state() private _items: BrokenLinkModel[] = [];
  @state() private _total = 0;
  @state() private _page = 1;
  @state() private _status?: LinkStatus;
  @state() private _version: LinkVersionFilter = 'All';
  @state() private _documentType = '';
  @state() private _orderBy: BrokenLinkOrder = 'Source';
  @state() private _descending = false;
  @state() private _loading = true;
  @state() private _error = false;
  @state() private _requestingScan = false;

  /** Incremented per request so responses of superseded requests are ignored. */
  #itemsRequest = 0;
  #summaryRequest = 0;
  #pollTimer?: ReturnType<typeof setTimeout>;
  #connected = false;

  override connectedCallback() {
    super.connectedCallback();
    this.#connected = true;
    void this.#loadSummary();
    void this.#loadItems();
  }

  override disconnectedCallback() {
    super.disconnectedCallback();
    this.#connected = false;
    clearTimeout(this.#pollTimer);
    // Invalidate in-flight requests so they do not update a detached element.
    this.#itemsRequest++;
    this.#summaryRequest++;
  }

  #filters() {
    return {
      status: this._status ? [this._status] : undefined,
      version: this._version,
      documentType: this._documentType || undefined,
      orderBy: this._orderBy,
      descending: this._descending,
    };
  }

  async #loadItems() {
    const requestId = ++this.#itemsRequest;
    this._loading = true;
    let items: BrokenLinkModel[] = [];
    let total = 0;
    let failed = false;
    try {
      const { data, error } = await LinkHealthService.getBrokenLinks({
        query: { ...this.#filters(), skip: (this._page - 1) * PAGE_SIZE, take: PAGE_SIZE },
      });
      failed = error !== undefined || data === undefined;
      items = data?.items ?? [];
      total = data?.total ?? 0;
    } catch {
      failed = true;
    }
    if (requestId !== this.#itemsRequest) return; // stale response
    this._items = items;
    this._total = total;
    this._error = failed;
    this._loading = false;
  }

  async #loadSummary() {
    const requestId = ++this.#summaryRequest;
    clearTimeout(this.#pollTimer);
    let summary: LinkHealthSummaryResponse | undefined;
    try {
      summary = (await LinkHealthService.getSummary()).data;
    } catch {
      summary = undefined;
    }
    if (requestId !== this.#summaryRequest || !this.#connected) return; // stale response or detached
    const wasBusy = this.#isBusy(this._summary);
    if (summary) this._summary = summary;
    if (this.#isBusy(this._summary)) {
      // Poll while a scan is requested or running; reload the list once it has finished.
      this.#pollTimer = setTimeout(() => void this.#loadSummary(), POLL_MS);
    } else if (wasBusy) {
      void this.#loadItems();
    }
  }

  #isBusy(summary?: LinkHealthSummaryResponse) {
    return summary !== undefined && summary.scan.phase !== 'Idle';
  }

  async #requestScan() {
    this._requestingScan = true;
    try {
      // A 409 means a scan is already running; the summary below shows its progress.
      await LinkHealthService.requestScan();
    } catch {
      // The summary shows the current state.
    }
    this._requestingScan = false;
    if (!this.#connected) return;
    await this.#loadSummary();
  }

  async #export() {
    let blob: Blob | undefined;
    try {
      const { data } = await LinkHealthService.exportBrokenLinks({ query: this.#filters(), parseAs: 'blob' });
      blob = data instanceof Blob ? data : undefined;
    } catch {
      blob = undefined;
    }
    if (!blob) {
      const notifications = await this.getContext(UMB_NOTIFICATION_CONTEXT);
      notifications?.peek('danger', { data: { message: this.localize.term('ddContentHealth_exportFailed') } });
      return;
    }
    const url = URL.createObjectURL(blob);
    const link = document.createElement('a');
    link.href = url;
    link.download = `broken-links-${new Date().toISOString().slice(0, 16).replace(/[-:]/g, '').replace('T', '-')}.csv`;
    link.click();
    URL.revokeObjectURL(url);
  }

  #applyFilter(change: () => void) {
    change();
    this._page = 1;
    void this.#loadItems();
  }

  #sort(orderBy: BrokenLinkOrder) {
    this.#applyFilter(() => {
      this._descending = this._orderBy === orderBy ? !this._descending : false;
      this._orderBy = orderBy;
    });
  }

  override render() {
    return html`
      <uui-box headline=${this.localize.term('ddContentHealth_title')}>
        ${this.#renderScan()} ${this.#renderTiles()} ${this.#renderFilters()} ${this.#renderList()}
      </uui-box>
    `;
  }

  #renderScan() {
    const summary = this._summary;
    if (!summary) return nothing;
    const scan = summary.scan;

    let text: string;
    if (scan.phase === 'Running') {
      text = this.localize.term('ddContentHealth_scanRunning', scan.documentsDone, scan.documentsTotal);
    } else if (scan.phase === 'Requested') {
      text = this.localize.term('ddContentHealth_scanRequested');
    } else if (scan.neverScanned) {
      text = this.localize.term('ddContentHealth_neverScanned');
    } else if (scan.completedUtc && scan.startedUtc) {
      const seconds = Math.max(0, Math.round((Date.parse(scan.completedUtc) - Date.parse(scan.startedUtc)) / 1000));
      const date = this.localize.date(scan.completedUtc, { dateStyle: 'medium', timeStyle: 'short' });
      text = this.localize.term('ddContentHealth_lastScan', date, seconds, scan.documentsTotal);
    } else {
      text = this.localize.term('ddContentHealth_scanIncomplete');
    }
    const progress = scan.documentsTotal > 0 ? Math.round((scan.documentsDone / scan.documentsTotal) * 100) : 0;

    return html`
      <div class="scan">
        <span>${text}</span>
        ${scan.phase === 'Idle' && scan.documentsFailed > 0
          ? html`<uui-tag color="warning">${this.localize.term('ddContentHealth_scanFailedDocuments', scan.documentsFailed)}</uui-tag>`
          : nothing}
        ${scan.lastError
          ? html`<uui-tag color="danger" title=${scan.lastError}>${this.localize.term('ddContentHealth_scanError')}</uui-tag>`
          : nothing}
        ${summary.canStartScan
          ? html`<uui-button
              look="secondary"
              label=${this.localize.term('ddContentHealth_scanNow')}
              ?disabled=${scan.phase !== 'Idle'}
              .state=${this._requestingScan ? 'waiting' : undefined}
              @click=${this.#requestScan}></uui-button>`
          : nothing}
      </div>
      ${scan.phase === 'Running' ? html`<uui-loader-bar progress=${progress}></uui-loader-bar>` : nothing}
    `;
  }

  #renderTiles() {
    const counts = this._summary?.counts ?? [];
    const live = counts.reduce((sum, c) => sum + c.live, 0);
    const draft = counts.reduce((sum, c) => sum + c.draft, 0);
    const total = live + draft;
    return html`
      <div class="tiles">
        ${counts.map(
          (c) => html`<button
            type="button"
            class="tile ${this._status === c.status ? 'active' : ''}"
            @click=${() => this.#applyFilter(() => (this._status = this._status === c.status ? undefined : c.status))}>
            <span class="count">${c.live + c.draft}</span>
            <span class="label">${this.localize.term(statusTerm(c.status))}</span>
            <span class="split">${this.localize.term('ddContentHealth_liveDraft', c.live, c.draft)}</span>
          </button>`,
        )}
        <button
          type="button"
          class="tile ${this._status === undefined ? 'active' : ''}"
          @click=${() => this.#applyFilter(() => (this._status = undefined))}>
          <span class="count">${total}</span>
          <span class="label">${this.localize.term('ddContentHealth_total')}</span>
          <span class="split">${this.localize.term('ddContentHealth_liveDraft', live, draft)}</span>
        </button>
      </div>
    `;
  }

  #renderFilters() {
    const documentTypes = this._summary?.documentTypes ?? [];
    return html`
      <div class="filters">
        <uui-select
          label=${this.localize.term('ddContentHealth_version')}
          .options=${VERSIONS.map((v) => ({ name: this.localize.term(v.term), value: v.value, selected: v.value === this._version }))}
          @change=${(e: UUISelectEvent) => this.#applyFilter(() => (this._version = (e.target.value as LinkVersionFilter) || 'All'))}></uui-select>
        <uui-select
          label=${this.localize.term('ddContentHealth_documentType')}
          .options=${[
            { name: this.localize.term('ddContentHealth_allDocumentTypes'), value: '', selected: this._documentType === '' },
            ...documentTypes.map((t) => ({ name: t.name, value: t.alias, selected: t.alias === this._documentType })),
          ]}
          @change=${(e: UUISelectEvent) => this.#applyFilter(() => (this._documentType = e.target.value as string))}></uui-select>
        <uui-button look="outline" label=${this.localize.term('ddContentHealth_exportCsv')} @click=${this.#export}></uui-button>
      </div>
    `;
  }

  #renderList() {
    if (this._loading && this._items.length === 0) return html`<uui-loader></uui-loader>`;
    if (this._error) return html`<p>${this.localize.term('ddContentHealth_loadFailed')}</p>`;
    if (this._items.length === 0) {
      const neverScanned = this._summary?.scan.neverScanned === true;
      return html`<p>${this.localize.term(neverScanned ? 'ddContentHealth_neverScanned' : 'ddContentHealth_empty')}</p>`;
    }
    const pages = Math.max(1, Math.ceil(this._total / PAGE_SIZE));
    return html`
      <uui-table>
        <uui-table-head>
          <uui-table-head-cell>${this.#sortHeader('Source', 'ddContentHealth_page')}</uui-table-head-cell>
          <uui-table-head-cell>${this.localize.term('ddContentHealth_property')}</uui-table-head-cell>
          <uui-table-head-cell>${this.localize.term('ddContentHealth_language')}</uui-table-head-cell>
          <uui-table-head-cell>${this.localize.term('ddContentHealth_target')}</uui-table-head-cell>
          <uui-table-head-cell>${this.#sortHeader('Status', 'ddContentHealth_status')}</uui-table-head-cell>
        </uui-table-head>
        ${repeat(
          this._items,
          (l) => `${l.sourceKey}|${l.propertyAlias}|${l.culture ?? ''}|${l.isDraft}|${l.targetKey}`,
          (l) => html`
            <uui-table-row>
              <uui-table-cell><a class="page-link" href=${documentHref(l.sourceKey)} title=${l.sourcePath}>${l.sourceName}</a></uui-table-cell>
              <uui-table-cell>${l.propertyName}</uui-table-cell>
              <uui-table-cell>${l.culture ?? '—'}</uui-table-cell>
              <uui-table-cell>${targetTemplate(this.localize, l)}</uui-table-cell>
              <uui-table-cell>
                <uui-tag color=${statusColor(l.status)}>${this.localize.term(statusTerm(l.status))}</uui-tag>
                <uui-tag look="outline">${this.localize.term(l.isDraft ? 'ddContentHealth_draft' : 'ddContentHealth_live')}</uui-tag>
              </uui-table-cell>
            </uui-table-row>
          `,
        )}
      </uui-table>
      ${pages > 1
        ? html`<uui-pagination
            .total=${pages}
            .current=${this._page}
            @change=${(e: UUIPaginationEvent) => {
              this._page = e.target.current;
              void this.#loadItems();
            }}></uui-pagination>`
        : nothing}
    `;
  }

  #sortHeader(orderBy: BrokenLinkOrder, term: string) {
    const arrow = this._orderBy === orderBy ? (this._descending ? ' ▼' : ' ▲') : '';
    return html`<button type="button" class="sort" @click=${() => this.#sort(orderBy)}>${this.localize.term(term)}${arrow}</button>`;
  }

  static override styles = [
    sharedStyles,
    css`
      :host {
        display: block;
        padding: var(--uui-size-layout-1);
      }
      .scan {
        display: flex;
        flex-wrap: wrap;
        align-items: center;
        gap: var(--uui-size-space-3);
        margin-bottom: var(--uui-size-space-3);
      }
      .scan uui-button,
      .filters uui-button {
        margin-left: auto;
      }
      .tiles {
        display: grid;
        grid-template-columns: repeat(auto-fit, minmax(160px, 1fr));
        gap: var(--uui-size-space-4);
        margin: var(--uui-size-space-4) 0;
      }
      .tile {
        display: flex;
        flex-direction: column;
        align-items: flex-start;
        gap: var(--uui-size-space-1);
        padding: var(--uui-size-space-4);
        border: 1px solid var(--uui-color-border);
        border-radius: var(--uui-border-radius);
        background: var(--uui-color-surface);
        color: inherit;
        font: inherit;
        text-align: left;
        cursor: pointer;
      }
      .tile:hover {
        border-color: var(--uui-color-border-emphasis);
      }
      .tile.active {
        border-color: var(--uui-color-selected);
        box-shadow: 0 0 0 1px var(--uui-color-selected);
      }
      .count {
        font-size: var(--uui-type-h3-size);
        font-weight: bold;
      }
      .split {
        color: var(--uui-color-text-alt);
        font-size: var(--uui-type-small-size);
      }
      .filters {
        display: flex;
        flex-wrap: wrap;
        align-items: center;
        gap: var(--uui-size-space-4);
        margin-bottom: var(--uui-size-space-4);
      }
      .sort {
        padding: 0;
        border: 0;
        background: none;
        color: inherit;
        font: inherit;
        font-weight: bold;
        cursor: pointer;
      }
      uui-pagination {
        display: block;
        margin-top: var(--uui-size-space-4);
      }
    `,
  ];
}

export default DdLinkHealthDashboardElement;
