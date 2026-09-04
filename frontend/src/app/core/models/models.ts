export interface User {
  id: string;
  discordUsername: string;
  avatarUrl?: string | null;
  riotGameName?: string | null;
  riotTagLine?: string | null;
}

export interface Club {
  id: string;
  name: string;
  slug: string;
  ownerUserId: string;
  inviteCode: string;
  createdAt: string;
}

export type ClubRole = 'Member' | 'Mod' | 'Admin' | 'Owner';

export interface ClubDetail extends Club {
  myRole: ClubRole;
  canManage: boolean;
}

export interface ClubMember {
  clubId: string;
  userId: string;
  discordUsername: string;
  avatarUrl?: string | null;
  role: ClubRole;
  status: 'Pending' | 'Approved';
  joinedAt: string;
}

export type LobbyStatus = 'Open' | 'Rolled' | 'Played';

export interface Lobby {
  id: string;
  clubId: string;
  createdByUserId: string;
  status: LobbyStatus;
  createdAt: string;
}

export type Team = 'Blue' | 'Red';

export interface LobbyPlayer {
  lobbyId: string;
  userId: string;
  discordUsername: string;
  avatarUrl?: string | null;
  assignedTeam?: Team | null;
  assignedChampionId?: number | null;
}

export interface LobbyState {
  lobby: Lobby;
  players: LobbyPlayer[];
}

export interface Champion {
  id: number;
  alias: string;
  name: string;
  title: string;
  iconUrl: string;
  loadingArtUrl: string;
  splashUrl: string;
}

export interface ChampionCatalog {
  version: string;
  champions: Champion[];
}
