// Unused, dead - old JWT-generation helper from before token generation
// moved to TokenService (see ITokenService/TokenService, used everywhere
// tokens are actually issued today). Was already fully commented-out with
// no live code, and also contained a hardcoded JWT signing secret in that
// dead code, which is one more reason not to leave it lying around even
// inert. Safe to delete this file manually - this workspace doesn't allow
// deleting files here directly.
