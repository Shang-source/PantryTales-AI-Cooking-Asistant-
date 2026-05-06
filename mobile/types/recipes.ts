export type RecipeVisibility = "Private" | "Public";

export type RecipeDifficulty = "None" | "Easy" | "Medium" | "Hard";

export type RecipeType = "User" | "System" | "Model";

export interface CreateRecipeIngredient {
  name: string;
  amount?: number | null;
  unit?: string | null;
  isOptional?: boolean;
  category?: string | null;
}

export interface RecipeNutrition {
  calories?: number | null;
  carbohydrates?: number | null;
  fat?: number | null;
  protein?: number | null;
  sugar?: number | null;
  sodium?: number | null;
  saturatedFat?: number | null;
}

export interface CreateRecipeRequest {
  title: string;
  description: string;
  steps: string[];
  visibility: RecipeVisibility;
  type?: RecipeType;
  imageUrls?: string[];
  tags?: string[];
  servings?: number | null;
  totalTimeMinutes?: number | null;
  difficulty?: RecipeDifficulty;
  ingredients?: CreateRecipeIngredient[];
  nutrition?: RecipeNutrition;
}

export interface RecipeIngredientDto {
  recipeIngredientId: string;
  ingredientId: string;
  name: string;
  amount: number | null;
  unit: string | null;
  isOptional: boolean;
  category?: string | null;
}

export interface RecipeAuthorDto {
  id: string;
  nickname: string;
  avatarUrl: string | null;
}

export interface RecipeDetailDto {
  id: string;
  householdId: string;
  authorId: string | null;
  author: RecipeAuthorDto | null;
  title: string;
  description: string;
  steps: string[];
  visibility: RecipeVisibility;
  type: RecipeType;
  imageUrls?: string[] | null;
  likesCount: number;
  likedByMe: boolean;
  commentsCount: number;
  savedCount: number;
  savedByMe: boolean;
  createdAt: string;
  updatedAt: string;
  ingredients: RecipeIngredientDto[];
  tags: string[];
  difficulty: RecipeDifficulty;
  servings?: number | null;
  totalTimeMinutes?: number | null;
  // Nutrition fields - Calories is actual value, others are % daily value
  calories?: number | null;
  carbohydrates?: number | null;
  fat?: number | null;
  protein?: number | null;
  sugar?: number | null;
  sodium?: number | null;
  saturatedFat?: number | null;
}

export interface RecipeCardDto {
  id: string;
  authorId: string | null;
  authorNickname: string;
  authorAvatarUrl?: string | null;
  title: string;
  description: string;
  coverImageUrl?: string | null;
  visibility: RecipeVisibility;
  type: RecipeType;
  likesCount: number;
  likedByMe?: boolean;
  commentsCount: number;
  savedCount: number;
  savedByMe?: boolean;
  createdAt: string;
  updatedAt: string;
  tags: string[];
}

export interface RecipeLikeResponse {
  recipeId: string;
  isLiked: boolean;
  likesCount: number;
}

export interface RecipeSaveResponse {
  recipeId: string;
  isSaved: boolean;
  savesCount?: number;
}

export interface MyLikedRecipeCardDto {
  id: string;
  title: string;
  description?: string | null;
  coverImageUrl?: string | null;
  authorId?: string | null;
  authorName: string;
  likesCount: number;
  likedByMe: boolean;
  likedAt: string;
}

export interface MeLikesCountDto {
  count: number;
}

export interface MySavedRecipeCardDto {
  id: string;
  title: string;
  description?: string | null;
  coverImageUrl?: string | null;
  authorId?: string | null;
  authorName: string;
  savedCount: number;
  savedByMe: boolean;
  savedAt: string;
  type: RecipeType;
}

export interface MeSavesCountDto {
  count: number;
}

// Cooking history types
export interface MyCookedRecipeCardDto {
  cookId: string; // Unique ID for this cook entry
  id: string; // Recipe ID
  title: string;
  description?: string | null;
  coverImageUrl?: string | null;
  authorId?: string | null;
  authorName: string;
  cookCount: number; // Number of times user cooked this recipe
  lastCookedAt: string; // Most recent cook time
  firstCookedAt: string; // First cook time
}

export interface MeCooksCountDto {
  count: number; // Total number of unique recipes cooked
}

export interface RecipeCookResponse {
  recipeId: string;
  cookId: string;
  cookCount: number; // Updated cook count for this recipe
  cookedAt: string;
}
