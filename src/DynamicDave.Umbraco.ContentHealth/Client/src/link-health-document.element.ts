import { css, html, customElement, state, repeat, nothing } from '@umbraco-cms/backoffice/external/lit';
import { UmbLitElement } from '@umbraco-cms/backoffice/lit-element';
import { UMB_DOCUMENT_WORKSPACE_CONTEXT } from '@umbraco-cms/backoffice/document';
import { LinkHealthService } from './api/index.js';
import type { DocumentLinkHealthResponse } from './api/index.js';
import { documentHref, sharedStyles, statusColor, statusTerm, targetTemplate } from './link-health-shared.js';

@customElement('dd-link-health-document')
export class DdLinkHealthDocumentElement extends UmbLitElement {
  @state() private _data?: DocumentLinkHealthResponse;
  @state() private _error = false;

  #key?: string;
  /** Incremented per load so out-of-order responses (and responses for a previous document) are ignored. */
  #requestId = 0;

  constructor() {
    super();
    this.consumeContext(UMB_DOCUMENT_WORKSPACE_CONTEXT, (ctx) => {
      // Observing under a fixed alias replaces the previous observer when the context changes.
      this.observe(
        ctx?.unique,
        (unique) => {
          if (unique && unique !== this.#key) {
            this.#key = unique;
            void this.#load();
          }
        },
        'ddContentHealthUnique',
      );
      // Changes after every save and publish. The server reindexes the document before it responds, so reloading
      // shows the new state.
      this.observe(
        ctx?.persistedData,
        (data) => {
          if (data && data.unique === this.#key) void this.#load();
        },
        'ddContentHealthPersisted',
      );
    });
  }

  override disconnectedCallback() {
    super.disconnectedCallback();
    // Invalidate in-flight requests so they do not update a detached element.
    this.#requestId++;
  }

  async #load() {
    const key = this.#key;
    if (!key) return;
    const requestId = ++this.#requestId;
    let data: DocumentLinkHealthResponse | undefined;
    try {
      const response = await LinkHealthService.getDocument({ path: { key } });
      data = response.error !== undefined ? undefined : response.data;
    } catch {
      data = undefined;
    }
    if (requestId !== this.#requestId) return; // stale response
    this._data = data;
    this._error = data === undefined;
  }

  override render() {
    if (this._error) return html`<uui-box><p>${this.localize.term('ddContentHealth_loadFailed')}</p></uui-box>`;
    if (!this._data) return html`<uui-loader></uui-loader>`;
    return html`
      <uui-box headline=${this.localize.term('ddContentHealth_outgoingTitle')}>${this.#renderOutgoing(this._data)}</uui-box>
      <uui-box headline=${this.localize.term('ddContentHealth_incomingTitle')}>${this.#renderIncoming(this._data)}</uui-box>
      <p class="hint">${this.localize.term('ddContentHealth_checkedHint')}</p>
    `;
  }

  #liveTag(isDraft: boolean) {
    return html`<uui-tag look="outline">${this.localize.term(isDraft ? 'ddContentHealth_draft' : 'ddContentHealth_live')}</uui-tag>`;
  }

  #renderOutgoing(data: DocumentLinkHealthResponse) {
    if (data.outgoing.length === 0) {
      return html`<p class="ok"><uui-icon name="icon-check"></uui-icon> ${this.localize.term('ddContentHealth_outgoingEmpty')}</p>`;
    }
    return html`
      <uui-table>
        <uui-table-head>
          <uui-table-head-cell>${this.localize.term('ddContentHealth_property')}</uui-table-head-cell>
          <uui-table-head-cell>${this.localize.term('ddContentHealth_language')}</uui-table-head-cell>
          <uui-table-head-cell>${this.localize.term('ddContentHealth_target')}</uui-table-head-cell>
          <uui-table-head-cell>${this.localize.term('ddContentHealth_status')}</uui-table-head-cell>
        </uui-table-head>
        ${repeat(
          data.outgoing,
          (l) => `${l.propertyAlias}|${l.culture ?? ''}|${l.isDraft}|${l.targetKey}`,
          (l) => html`
            <uui-table-row>
              <uui-table-cell>${l.propertyName}</uui-table-cell>
              <uui-table-cell>${l.culture ?? '—'}</uui-table-cell>
              <uui-table-cell>${targetTemplate(this.localize, l)}</uui-table-cell>
              <uui-table-cell>
                <uui-tag color=${statusColor(l.status)}>${this.localize.term(statusTerm(l.status))}</uui-tag>
                ${this.#liveTag(l.isDraft)}
              </uui-table-cell>
            </uui-table-row>
          `,
        )}
      </uui-table>
    `;
  }

  #renderIncoming(data: DocumentLinkHealthResponse) {
    const hidden = data.incomingHiddenCount > 0
      ? html`<p class="hint">${this.localize.term('ddContentHealth_incomingHidden', data.incomingHiddenCount)}</p>`
      : nothing;
    if (data.incoming.length === 0) {
      return html`<p>${this.localize.term('ddContentHealth_incomingEmpty')}</p>${hidden}`;
    }
    return html`
      <uui-table>
        <uui-table-head>
          <uui-table-head-cell>${this.localize.term('ddContentHealth_page')}</uui-table-head-cell>
          <uui-table-head-cell>${this.localize.term('ddContentHealth_property')}</uui-table-head-cell>
          <uui-table-head-cell>${this.localize.term('ddContentHealth_language')}</uui-table-head-cell>
          <uui-table-head-cell>${this.localize.term('ddContentHealth_status')}</uui-table-head-cell>
        </uui-table-head>
        ${repeat(
          data.incoming,
          (l) => `${l.sourceKey}|${l.propertyName}|${l.culture ?? ''}|${l.isDraft}`,
          (l) => html`
            <uui-table-row>
              <uui-table-cell><a class="page-link" href=${documentHref(l.sourceKey)}>${l.sourceName}</a></uui-table-cell>
              <uui-table-cell>${l.propertyName}</uui-table-cell>
              <uui-table-cell>${l.culture ?? '—'}</uui-table-cell>
              <uui-table-cell>
                ${l.status !== 'Ok'
                  ? html`<uui-tag color=${statusColor(l.status)}>${this.localize.term(statusTerm(l.status))}</uui-tag>`
                  : nothing}
                ${this.#liveTag(l.isDraft)}
              </uui-table-cell>
            </uui-table-row>
          `,
        )}
      </uui-table>
      ${hidden}
    `;
  }

  static override styles = [
    sharedStyles,
    css`
      :host {
        display: block;
        padding: var(--uui-size-layout-1);
      }
      uui-box + uui-box {
        margin-top: var(--uui-size-layout-1);
      }
      .ok {
        display: flex;
        align-items: center;
        gap: var(--uui-size-space-2);
        color: var(--uui-color-positive);
      }
      .hint {
        color: var(--uui-color-text-alt);
        font-size: var(--uui-type-small-size);
      }
    `,
  ];
}

export default DdLinkHealthDocumentElement;
