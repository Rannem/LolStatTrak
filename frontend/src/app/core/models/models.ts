export interface User {
  id: string;
  discordUsername: string;
  avatarUrl?: string;
  riotGameName?: string;
  riotTagLine?: string;
}

export interface Club {
  id: string;
  name: string;
  slug: string;
  ownerUserId: string;
  inviteCode: string;
  createdAt: string;
}

export interface ClubMember {
  clubId: string;
  userId: string;
  role: 'Member' | 'Mod' | 'Admin' | 'Owner' | number;
  status: 'Pending' | 'Approved' | number;
  joinedAt: string;
}

export interface Lobby {
  id: string;
  clubId: string;
  createdByUserId: string;
  status: number;
  createdAt: string;
}

export interface LobbyPlayer {
  lobbyId: string;
  userId: string;
  assignedTeam?: number | null;
  assignedChampionId?: number | null;
}
