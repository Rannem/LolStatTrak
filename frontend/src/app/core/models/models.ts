export type AccessStatus = 'Pending' | 'Approved' | 'Rejected';

export interface User {
  id: string;
  discordUsername: string;
  avatarUrl?: string | null;
  riotGameName?: string | null;
  riotTagLine?: string | null;
  riotLinked: boolean;
  isGlobalAdmin: boolean;
  accessStatus: AccessStatus;
}

export interface AdminUser extends Omit<User, 'riotLinked'> {
  createdAt: string;
}

export interface Club {
  id: string;
  name: string;
  slug: string;
  ownerUserId: string;
  inviteCode: string;
  createdAt: string;
}

export interface ClubOverview extends Club {
  ownerUsername: string;
  memberCount: number;
  matchCount: number;
}

export type ClubRole = 'Member' | 'Mod' | 'Admin' | 'Owner';

export interface ClubDetail extends Club {
  myRole: ClubRole | null;
  isGlobalAdmin: boolean;
  canManage: boolean;
  canAdminister: boolean;
  isOwner: boolean;
}

export interface ClubMember {
  clubId: string;
  userId: string;
  discordUsername: string;
  avatarUrl?: string | null;
  riotGameName?: string | null;
  riotTagLine?: string | null;
  role: ClubRole;
  status: 'Pending' | 'Approved';
  joinedAt: string;
}

export interface MatchParticipant {
  matchId: string;
  userId: string;
  discordUsername: string;
  avatarUrl?: string | null;
  championId: number;
  team: Team;
  kills: number;
  deaths: number;
  assists: number;
  win: boolean;
}

export interface MatchSummary {
  id: string;
  clubId: string;
  lobbyId?: string | null;
  riotMatchId: string;
  playedAt: string;
  queueId: number;
  participants: MatchParticipant[];
}

export interface AuditEntry {
  id: string;
  clubId?: string | null;
  clubName?: string | null;
  actorUserId?: string | null;
  actorUsername?: string | null;
  action: string;
  targetType?: string | null;
  targetId?: string | null;
  details?: string | null;
  createdAt: string;
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
  riotGameName?: string | null;
  riotTagLine?: string | null;
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
