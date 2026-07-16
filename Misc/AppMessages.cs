namespace Aniki.Misc;

internal sealed record UserListStatusChangedMessage(int AnimeId, AnimeStatus? Status);
