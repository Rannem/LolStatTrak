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
    this.http.get<ChampionCatalog>(`${environment.apiBaseUrl}/champions`).subscribe({
      next: (catalog) => this._catalog.set(catalog),
      complete: () => (this.loading = false),
      error: () => (this.loading = false),
    });
  }

  get(id: number | null | undefined): Champion | undefined {
    return id == null ? undefined : this.byId().get(id);
  }
}
