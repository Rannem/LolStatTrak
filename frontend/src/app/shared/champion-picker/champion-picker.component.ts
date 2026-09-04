import { Component, computed, inject, input, output, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { ChampionService } from '../../core/services/champion.service';

/**
 * Searchable grid of champion icons. Clicking toggles a champion in/out of the
 * selected set; the parent owns the actual selection state.
 */
@Component({
  selector: 'app-champion-picker',
  standalone: true,
  imports: [FormsModule],
  template: `
    <div class="picker-toolbar">
      <input
        type="search"
        [ngModel]="query()"
        (ngModelChange)="query.set($event)"
        placeholder="Search champions…"
        [disabled]="readonly()" />
      <span class="muted count">{{ selected().length }} banned · {{ champions.champions().length }} total</span>
    </div>

    @if (!champions.loaded()) {
      <div class="empty">Loading champions from Data Dragon…</div>
    } @else {
      <div class="picker-grid">
        @for (champ of filtered(); track champ.id) {
          <button
            type="button"
            class="champ"
            [class.banned]="isSelected(champ.id)"
            [disabled]="readonly()"
            [title]="champ.name + ' — ' + champ.title"
            (click)="toggle(champ.id)">
            <img [src]="champ.iconUrl" [alt]="champ.name" loading="lazy" />
            <span class="name">{{ champ.name }}</span>
            @if (isSelected(champ.id)) {
              <span class="x">✕</span>
            }
          </button>
        }
      </div>
    }
  `,
  styles: `
    .picker-toolbar {
      display: flex;
      gap: 1rem;
      align-items: center;
      margin-bottom: 1rem;

      input {
        max-width: 320px;
      }
    }

    .count {
      font-size: 0.8rem;
      white-space: nowrap;
    }

    .picker-grid {
      display: grid;
      grid-template-columns: repeat(auto-fill, minmax(64px, 1fr));
      gap: 0.5rem;
      max-height: 420px;
      overflow-y: auto;
      padding: 0.25rem;
      scrollbar-color: var(--gold-4) transparent;
    }

    .champ {
      all: unset;
      position: relative;
      cursor: pointer;
      display: flex;
      flex-direction: column;
      align-items: center;
      gap: 0.25rem;
      padding: 0.3rem 0.1rem;
      border-radius: var(--radius);
      transition: transform 0.12s;

      img {
        width: 56px;
        height: 56px;
        border-radius: 6px;
        border: 2px solid var(--gold-4);
        transition: filter 0.15s, border-color 0.15s, box-shadow 0.15s;
      }

      .name {
        font-size: 0.62rem;
        color: var(--text-muted);
        max-width: 64px;
        white-space: nowrap;
        overflow: hidden;
        text-overflow: ellipsis;
      }

      &:hover:not(:disabled) {
        transform: translateY(-2px);

        img {
          border-color: var(--gold-2);
          box-shadow: var(--glow-gold);
        }
      }

      &:disabled {
        cursor: default;
      }

      &.banned img {
        filter: grayscale(1) brightness(0.45);
        border-color: var(--danger);
      }

      &.banned .name {
        color: var(--danger);
        text-decoration: line-through;
      }

      .x {
        position: absolute;
        top: 0.35rem;
        left: 50%;
        transform: translate(-50%, 14px);
        font-size: 1.3rem;
        font-weight: 700;
        color: var(--danger);
        text-shadow: 0 0 8px rgba(0, 0, 0, 0.9);
        pointer-events: none;
      }
    }
  `,
})
export class ChampionPickerComponent {
  protected readonly champions = inject(ChampionService);

  readonly selected = input<number[]>([]);
  readonly readonly = input(false);
  readonly selectedChange = output<number[]>();

  protected readonly query = signal('');

  protected readonly filtered = computed(() => {
    const q = this.query().trim().toLowerCase();
    const all = this.champions.champions();
    return q ? all.filter((c) => c.name.toLowerCase().includes(q) || c.alias.toLowerCase().includes(q)) : all;
  });

  constructor() {
    this.champions.ensureLoaded();
  }

  protected isSelected(id: number): boolean {
    return this.selected().includes(id);
  }

  protected toggle(id: number): void {
    const next = this.isSelected(id) ? this.selected().filter((x) => x !== id) : [...this.selected(), id];
    this.selectedChange.emit(next);
  }
}
