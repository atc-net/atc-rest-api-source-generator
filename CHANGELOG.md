# Changelog

## [2.1.0](https://github.com/atc-net/atc-rest-api-source-generator/compare/v2.0.0...v2.1.0) (2026-09-24)


### New features

* **security:** map http bearer schemes onto the "Bearer" authentication scheme ([9d31382](https://github.com/atc-net/atc-rest-api-source-generator/commit/9d3138233e45176b480dbf98714c361eef7a1c3c))


### Bug fixes

* **security:** generate AddApiSecurityPolicies whenever a security scheme exists ([947388e](https://github.com/atc-net/atc-rest-api-source-generator/commit/947388ebc11767a1b2483e1f82de2aa15fb122f3))
* **validator:** only report OPR022 for a 403 on an anonymous operation ([54f21e1](https://github.com/atc-net/atc-rest-api-source-generator/commit/54f21e1e0bea8d235a2f4df6c34a306d20f1e00a))

## [2.0.0](https://github.com/atc-net/atc-rest-api-source-generator/compare/v1.0.393...v2.0.0) (2026-09-16)


### ⚠ BREAKING CHANGES

* **validator:** `nullable: true` is no longer honoured on a schema that declares no `type` of its own - typically a property using `allOf` or `oneOf`. Such properties are no longer generated as nullable.

### New features

* **generator:** warn when output caching cannot cache, and unify HttpMethod emission ([5400ac8](https://github.com/atc-net/atc-rest-api-source-generator/commit/5400ac813c78d9a4cfeea2470b43f3d3fe49e891))
* honour the declared webhook verb, and validate QUERY operation shape ([7b35d97](https://github.com/atc-net/atc-rest-api-source-generator/commit/7b35d97a1f09cfc40d622ffdd3f48c00108ec93f))
* move to Microsoft.OpenApi 3.10.2 and make the specs spec-valid ([e2132c5](https://github.com/atc-net/atc-rest-api-source-generator/commit/e2132c54c5371c4863006ce2fe1f167a3c207a63))
* **ts-client:** emit a union type for multi-ref oneOf/anyOf properties ([7fb6654](https://github.com/atc-net/atc-rest-api-source-generator/commit/7fb6654c6f09a922e1951833f8f038808035216c))
* **ts-client:** treat OpenAPI 3.2 QUERY as a read across the TypeScript hooks ([0a8ee45](https://github.com/atc-net/atc-rest-api-source-generator/commit/0a8ee45c22effb21e2fb5b0a91d547f204aeb940))
* **validator:** report the constructs that change meaning between OpenAPI versions ([59df400](https://github.com/atc-net/atc-rest-api-source-generator/commit/59df4006cea91a0e3890e3f5dc0b56d31ea256d2))
* **validator:** warn when a GET query array cannot fit in the URL, and demo QUERY in Showcase ([7564232](https://github.com/atc-net/atc-rest-api-source-generator/commit/75642329aa381564ffbee89a451f6844513c3310))


### Bug fixes

* **openapi:** honour 'nullable: true' in 3.1+ documents regardless of reader leniency ([003e3fc](https://github.com/atc-net/atc-rest-api-source-generator/commit/003e3fc93aef09610c856f073b35ce21373a042c))
* **openapi:** treat a 3.0 nullable-object branch as a null marker ([21d81e9](https://github.com/atc-net/atc-rest-api-source-generator/commit/21d81e9075cec494989eb675b43005d85ef630e0))
* point diagnostic help links at the wiki that actually has the docs ([baab3bf](https://github.com/atc-net/atc-rest-api-source-generator/commit/baab3bf0e1d8cc40160092f9f024481e550a15ea))
* **ts-client:** stop emitting imports the generated file never uses ([57e1ae4](https://github.com/atc-net/atc-rest-api-source-generator/commit/57e1ae450e19c08f271ad8bf438cbf86da128af5))
* **validator:** stop OPR008/OPR009 arguing with correctly named bulk actions ([4fb757c](https://github.com/atc-net/atc-rest-api-source-generator/commit/4fb757cb61202668ba6e69e35c343617eeffdfce))

## [1.0.393](https://github.com/atc-net/atc-rest-api-source-generator/compare/v1.0.392...v1.0.393) (2026-09-10)


### chore

* release 1.0.393 ([b8f14ea](https://github.com/atc-net/atc-rest-api-source-generator/commit/b8f14ea201965c932b3060b7448a5fbfbf19d4d5))
