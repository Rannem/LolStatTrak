import { DatePipe } from '@angular/common';
import { Component, input } from '@angular/core';
import { AuditEntry } from '../../core/models/models';

const LABELS: Record<string, string> = {
  'user.registered': 'registered an account',
  'user.approved': 'approved sign-up of',
  'user.rejected': 'rejected sign-up of',
  'user.deleted': 'deleted user',
  'user.riot_linked': 'linked a Riot account',
  'user.riot_unlinked': 'unlinked their Riot account',
  'club.created': 'created the club',
  'club.deleted': 'deleted club',
  'member.joined_via_invite': 'joined via invite code',
  'member.join_requested': 'requested to join',
  'member.approved': 'approved join request of',
  'member.role_changed': 'changed role of',
  'member.removed': 'removed member',
  'member.left': 'left the club',
  'bans.updated': 'updated the ban list',
  'lobby.created': 'started a lobby',
  'lobby.rolled': 'rolled a lobby',
  'lobby.marked_played': 'marked a lobby as played',
  'lobby.deleted': 'deleted a lobby',
  'match.deleted': 'deleted a tracked match',
};

@Component({
  selector: 'app-audit-log',
  standalone: true,
  imports: [DatePipe],
  template: `
    @if (entries().length === 0) {
      <div class="empty">Nothing logged yet.</div>
    } @else {
      <ul class="clean log">
        @for (e of entries(); track e.id) {
          <li>
            <span class="when dim">{{ e.createdAt | date: 'MMM d, HH:mm' }}</span>
            <span class="what">
              <strong>{{ e.actorUsername ?? 'System' }}</strong>
              {{ label(e.action) }}
              @if (showClub() && e.clubName) {
                <span class="muted">in {{ e.clubName }}</span>
              }
              @if (detail(e); as d) {
                <span class="detail dim">{{ d }}</span>
              }
            </span>
            <span class="badge action" [class.red]="isDestructive(e.action)">{{ e.action }}</span>
          </li>
        }
      </ul>
    }
  `,
  styles: `
    .log li {
      display: grid;
      grid-template-columns: 96px 1fr auto;
      gap: 0.75rem;
      align-items: baseline;
      padding: 0.5rem 0;
      font-size: 0.85rem;

      @media (max-width: 640px) {
        grid-template-columns: 1fr;
        gap: 0.2rem;
      }
    }
    .log li + li {
      border-top: 1px solid var(--border);
    }
    .when {
      font-size: 0.75rem;
      white-space: nowrap;
    }
    .detail {
      margin-left: 0.4rem;
      font-size: 0.75rem;
    }
    .action {
      font-size: 0.6rem;
    }
  `,
})
export class AuditLogComponent {
  readonly entries = input<AuditEntry[]>([]);
  readonly showClub = input(false);

  protected label(action: string): string {
    return LABELS[action] ?? action;
  }

  protected isDestructive(action: string): boolean {
    return action.endsWith('.deleted') || action.endsWith('.removed') || action.endsWith('.rejected');
  }

  protected detail(e: AuditEntry): string | null {
    if (!e.details) return null;
    try {
      const d = JSON.parse(e.details) as Record<string, unknown>;
      const parts: string[] = [];
      if (typeof d['DiscordUsername'] === 'string') parts.push(d['DiscordUsername'] as string);
      if (typeof d['Name'] === 'string') parts.push(`"${d['Name']}"`);
      if (typeof d['Role'] === 'string') parts.push(`→ ${d['Role']}`);
      if (typeof d['Count'] === 'number') parts.push(`(${d['Count']} banned)`);
      if (typeof d['Players'] === 'number') parts.push(`(${d['Players']} players)`);
      if (typeof d['RiotMatchId'] === 'string') parts.push(d['RiotMatchId'] as string);
      if (typeof d['GameName'] === 'string') parts.push(`${d['GameName']}#${d['TagLine'] ?? ''}`);
      if (d['MatchFound'] === false) parts.push('(no Riot match found)');
      return parts.length ? parts.join(' ') : null;
    } catch {
      return null;
    }
  }
}
