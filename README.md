# Virto Commerce Catalog Experience API (xCatalog) Module

[![CI status](https://github.com/VirtoCommerce/vc-module-x-catalog/workflows/Module%20CI/badge.svg?branch=dev)](https://github.com/VirtoCommerce/vc-module-x-catalog/actions?query=workflow%3A"Module+CI") [![Quality gate](https://sonarcloud.io/api/project_badges/measure?project=VirtoCommerce_vc-module-x-catalog&metric=alert_status&branch=dev)](https://sonarcloud.io/dashboard?id=VirtoCommerce_vc-module-x-catalog) [![Reliability rating](https://sonarcloud.io/api/project_badges/measure?project=VirtoCommerce_vc-module-x-catalog&metric=reliability_rating&branch=dev)](https://sonarcloud.io/dashboard?id=VirtoCommerce_vc-module-x-catalog) [![Security rating](https://sonarcloud.io/api/project_badges/measure?project=VirtoCommerce_vc-module-x-catalog&metric=security_rating&branch=dev)](https://sonarcloud.io/dashboard?id=VirtoCommerce_vc-module-x-catalog) [![Sqale rating](https://sonarcloud.io/api/project_badges/measure?project=VirtoCommerce_vc-module-x-catalog&metric=sqale_rating&branch=dev)](https://sonarcloud.io/dashboard?id=VirtoCommerce_vc-module-x-catalog)

The xCatalog module provides high-performance search queries for catalog data directly from the search index engine.

## Filter syntax extensions

The `filter` argument of the `products` query accepts, besides the index fields themselves, virtual terms that the
module rewrites before the request reaches the index.

### `barcode:"<value>"`

A code scanned in the storefront, e.g. `filter: "barcode:\"0123456789012\""`.

The term is replaced with an exact term filter over the product index fields the store has configured in the Catalog
module setting `Catalog.Search.BarcodeSearchFields` (*Store → Search configuration → Barcode scanner* in the admin UI):
one field gives one term filter, several fields are combined with OR (e.g. `gtin` OR `manufacturerPartNumber`). Each of
those per-field term filters is reported back as a generated filter rather than as one of the caller's own filters.

The document scope is widened from products to variations, so a code stored on a variation is found - but only when the
default `is:product` scope is in effect; a request that sends a scope of its own keeps it. Only the first `barcode`
term of a request is expanded; a second one stays a literal field name.

With no configured field the store matches a scanned code by full text: the term is left untouched (the storefront
sends the scanned value as an ordinary keyword in that mode instead of as this filter).

## Documentation

* [xCatalog module documentation](https://docs.virtocommerce.org/platform/developer-guide/GraphQL-Storefront-API-Reference-xAPI/Catalog/overview/)
* [View on GitHub](https://github.com/VirtoCommerce/vc-module-x-catalog)
* [Experience API Documentation](https://docs.virtocommerce.org/platform/developer-guide/GraphQL-Storefront-API-Reference-xAPI/)
* [Getting started](https://docs.virtocommerce.org/platform/developer-guide/GraphQL-Storefront-API-Reference-xAPI/getting-started/)
* [How to use GraphiQL](https://docs.virtocommerce.org/platform/developer-guide/GraphQL-Storefront-API-Reference-xAPI/graphiql/)
* [How to use Postman](https://docs.virtocommerce.org/platform/developer-guide/GraphQL-Storefront-API-Reference-xAPI/postman/)
* [How to extend](https://docs.virtocommerce.org/platform/developer-guide/GraphQL-Storefront-API-Reference-xAPI/x-api-extensions/)
* [Virto Commerce Frontend architecture](https://docs.virtocommerce.org/storefront/developer-guide/architecture/)


## References

* [Deployment](https://docs.virtocommerce.org/platform/developer-guide/Tutorials-and-How-tos/Tutorials/deploy-module-from-source-code/)
* [Installation](https://docs.virtocommerce.org/platform/user-guide/modules-installation/)
* [Home](https://virtocommerce.com)
* [Community](https://www.virtocommerce.org)
* [Download latest release](https://github.com/VirtoCommerce/vc-module-x-catalog/releases/latest)


## License
Copyright (c) Virto Solutions LTD.  All rights reserved.

Licensed under the Virto Commerce Open Software License (the "License"); you
may not use this file except in compliance with the License. You may
obtain a copy of the License at http://virtocommerce.com/opensourcelicense

Unless required by applicable law or agreed to in writing, software
distributed under the License is distributed on an "AS IS" BASIS,
WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or
implied.
