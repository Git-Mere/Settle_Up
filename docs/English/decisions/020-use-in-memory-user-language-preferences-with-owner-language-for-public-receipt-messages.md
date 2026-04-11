# 020 - Use In-Memory User Language Preferences With Owner Language For Public Receipt Messages

## Status
Accepted

## Context
To add Korean/English language switching to `discord-api`, the project needed both a `/language` command and a separated UI-string structure.

This decision had to resolve two key issues.

1. the public receipt main message is shared by multiple users
2. private, ephemeral, and history messages can be shown differently per caller

Under the Discord interaction model, one public message cannot be rendered differently for each user. That meant that once user-specific language preferences were introduced, a separate rule was required for which language the public main message should use.

There were additional constraints as well.

- the normal user flow should work without introducing a complex persistent storage dependency just for language settings
- slash command descriptions and option descriptions are registered metadata and are hard to localize per user
- operational logs and exception messages are better kept in English for consistency and searchability

## Options Considered
1. keep a single default language with no language setting
- simplest implementation
- but cannot support mixed Korean/English user environments

2. persist per-user language settings durably and try to render public messages differently for each user
- preserves user preference well
- but Discord public messages cannot actually be rendered per user
- adds too much storage and synchronization complexity for the current project stage

3. keep per-user language preferences, but fix the public receipt message to the owner's language
- the public message language stays stable during the session
- private, ephemeral, and history responses can still use the caller's language
- implementation remains relatively simple

4. keep per-user language preferences, but let the public receipt language follow the last user who changed `/language`
- technically possible
- but any participant could keep changing the shared public message language, making the UX unstable

## Decision
Implement language switching with the following policy.

- add the `/language` slash command
- support `English` and `Korean`
- keep user language preference in memory
- use `English` as the default language
- use the caller's language for private, ephemeral, and history responses
- use the owner's language for the public receipt main message
- if the owner changes `/language`, immediately refresh any in-progress public receipt message they own
- register slash command descriptions and option descriptions in simple English
- keep logs and exception messages in English

## Consequences
Positive outcomes:

- multilingual UI can be added quickly without introducing Cosmos or another persistence dependency just for preferences
- public messages stay stable because the owner determines the shared language
- each participant can still see private and ephemeral UI in their own preferred language
- keeping logs and exceptions in English improves operational consistency and searchability

Negative outcomes and costs:

- user language settings are lost when the bot restarts
- the public main message can only have one language at a time because it is owner-based
- slash command metadata remains English rather than per-user localized

## Follow-up Notes
- if language settings later need to survive restart, a separate persistence decision should be added
- the owner-language policy is based on the shared-rendering constraint of Discord public messages
- this decision also pushed `discord-api` toward a dedicated localization layer for UI strings
