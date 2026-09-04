import { HttpClient } from '@angular/common/http';
import { Injectable, computed, inject, signal } from '@angular/core';
import { environment } from '../../../environments/environment';
import { Champion, ChampionCatalog } from '../models/models';

/** Loads the Data Dragon champion list once per session and exposes fast id lookups. */
@Injectable({ providedIn: 'root' })
export class ChampionService {
  private readonly http = inject(HttpClient);

  private readonly _catalog = signal<ChampionCatalog | null>(null);
  private loading = false;

  readonly champions = computed(() => this._catalog()?.champions ?? []);
  readonly version = computed(() => this._catalog()?.version ?? '');
  readonly byId = computed(() => new Map(this.champions().map((c) => [c.id, c])));
  readonly loaded = computed(() => this._catalog() !== null);

  ensureLoaded(): void {
    if (this._catalog() || this.loading) return;
    this.loading = true;

    // Seed synchronously from the last known catalog so names/icons are on first paint,
    // then revalidate (the server answers 304 via ETag when nothing changed).
    const seed = this.readSeed();
    if (seed) this._catalog.set(seed);

    this.http.get<ChampionCatalog>(`${environment.apiBaseUrl}/champions`).subscribe({
      next: (catalog) => {
        if (catalog.version !== seed?.version) {
          this._catalog.set(catalog);
          this.writeSeed(catalog);
        }
      },
      complete: () => (this.loading = false),
      error: () => (this.loading = false),
    });
  }

  get(id: number | null | undefined): Champion | undefined {
    return id == null ? undefined : this.byId().get(id);
  }

  private static readonly SEED_KEY = 'lst.champions';

  private readSeed(): ChampionCatalog | null {
    try {
      const raw = localStorage.getItem(ChampionService.SEED_KEY);
      if (!raw) return null;
      const parsed = JSON.parse(raw) as ChampionCatalog;
      return parsed?.version && Array.isArray(parsed.champions) ? parsed : null;
    } catch {
      return null;
    }
  }

  private writeSeed(catalog: ChampionCatalog): void {
    try {
      localStorage.setItem(ChampionService.SEED_KEY, JSON.stringify(catalog));
    } catch {
      // Quota / private mode — the HTTP cache still covers us.
    }
  }
}
