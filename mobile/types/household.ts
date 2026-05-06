// Corresponds to backend Dtos/Households/HouseholdInvitationDtos.cs

export interface InviteHouseholdMemberRequest {
  clerkUserId?: string;
  email?: string;
  expirationDays?: number;
}

export interface HouseholdInvitationResponseDto {
  id: string;
  householdId: string;
  email: string | null;
  status: string;
  expiredAt: string;
  createdAt: string;
  invitationType: "email" | "link";
  token?: string | null;
}

export interface CreateLinkInvitationRequest {
  expirationDays?: number;
}

// Corresponds to backend Dtos/Households/HouseholdMembershipDtos.cs

export interface HouseholdMembershipDto {
  householdId: string;
  householdName: string;
  role: string;
  joinedAt: string;
  ownerId: string;
  isOwner: boolean;
}

export interface HouseholdMembershipListDto {
  activeHouseholdId?: string;
  activeHouseholdName?: string;
  memberships: HouseholdMembershipDto[];
}

// Household member detail - for listing members in a household
export type HouseholdMemberStatus = "owner" | "joined" | "pending";

export interface HouseholdMemberDetailDto {
  id: string;
  displayName: string;
  email: string;
  avatarUrl?: string | null;
  status: HouseholdMemberStatus;
  joinedAt: string;
}

export interface HouseholdMembersListDto {
  householdId: string;
  householdName: string;
  activeMemberCount: number;
  pendingCount: number;
  members: HouseholdMemberDetailDto[];
}

// Response when leaving a household
export interface LeaveHouseholdResponseDto {
  activeHouseholdId: string;
  activeHouseholdName: string;
  isNewlyCreatedDefault: boolean;
}
