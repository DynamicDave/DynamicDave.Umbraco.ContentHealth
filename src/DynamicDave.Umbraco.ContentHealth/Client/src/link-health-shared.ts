import { css, html } from '@umbraco-cms/backoffice/external/lit';
import type { UmbLocalizationController } from '@umbraco-cms/backoffice/localization-api';
import type { LinkStatus, LinkTargetType } from './api/index.js';

export const documentHref = (key: string) => `/umbraco/section/content/workspace/document/edit/${key}`;

const mediaHref = (key: string) => `/umbraco/section/media/workspace/media/edit/${key}`;

export function statusColor(status: LinkStatus): 'danger' | 'warning' | 'default' {
  if (status === 'Missing' || status === 'Trashed') return 'danger';
  return status === 'Unpublished' ? 'warning' : 'default';
}

export const statusTerm = (status: LinkStatus) => `ddContentHealth_status${status}`;

/** The link target with a link to it, or "Deleted item" when it no longer exists. */
export function targetTemplate(
  localize: UmbLocalizationController,
  link: { targetKey: string; targetType: LinkTargetType; targetName?: string | null },
) {
  if (!link.targetName) return html`<em>${localize.term('ddContentHealth_deletedItem')}</em>`;
  const media = link.targetType === 'Media';
  return html`<a class="target-link" href=${media ? mediaHref(link.targetKey) : documentHref(link.targetKey)}>
    <uui-icon name=${media ? 'icon-picture' : 'icon-document'}></uui-icon> ${link.targetName}
  </a>`;
}

export const sharedStyles = css`
  .page-link,
  .target-link {
    color: var(--uui-color-interactive);
    text-decoration: none;
  }
  .page-link {
    font-weight: bold;
  }
  .page-link:hover,
  .target-link:hover {
    text-decoration: underline;
  }
  uui-tag + uui-tag {
    margin-left: var(--uui-size-space-2);
  }
`;
