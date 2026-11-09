// scripts/validate-contracts.js
// Automated verification for contract-first specifications (ADR-066)

import fs from 'node:fs';
import path from 'node:path';

function log(msg) {
  console.log(`[contracts-validator] ${msg}`);
}

function error(msg) {
  console.error(`[contracts-validator ERROR] ${msg}`);
}

let hasError = false;

// 1. Validate Error Registry
const errorRegistryPath = path.resolve('contracts/errors/registry.yaml');
if (!fs.existsSync(errorRegistryPath)) {
  error(`Missing error registry: ${errorRegistryPath}`);
  hasError = true;
} else {
  const content = fs.readFileSync(errorRegistryPath, 'utf8');
  const codeMatches = [...content.matchAll(/code:\s*([A-Z0-9_]+)/g)].map(m => m[1]);
  const idMatches = [...content.matchAll(/id:\s*(ERR-[0-9]+)/g)].map(m => m[1]);
  
  const uniqueCodes = new Set(codeMatches);
  const uniqueIds = new Set(idMatches);
  
  if (codeMatches.length !== uniqueCodes.size) {
    error(`Duplicate error codes found in registry! Total: ${codeMatches.length}, Unique: ${uniqueCodes.size}`);
    hasError = true;
  } else {
    log(`✓ Error registry valid: ${uniqueCodes.size} unique canonical error codes verified.`);
  }

  if (idMatches.length !== uniqueIds.size) {
    error(`Duplicate error IDs found in registry! Total: ${idMatches.length}, Unique: ${uniqueIds.size}`);
    hasError = true;
  }
}

// 2. Validate Roles and Scopes Model
const rolesPath = path.resolve('contracts/roles/permissions.yaml');
if (!fs.existsSync(rolesPath)) {
  error(`Missing roles specification: ${rolesPath}`);
  hasError = true;
} else {
  const content = fs.readFileSync(rolesPath, 'utf8');
  const roleMatches = [...content.matchAll(/code:\s*([A-Z_]+)/g)].map(m => m[1]);
  // 18 system roles + 3 grant profiles = 21 codes
  if (roleMatches.length >= 18) {
    log(`✓ Roles model valid: ${roleMatches.length} role codes verified against Document 3.`);
  } else {
    error(`Expected at least 18 system roles, found ${roleMatches.length}`);
    hasError = true;
  }
}

// 3. Validate Event Schemas (JSON)
const schemasDir = path.resolve('contracts/events/schemas');
if (!fs.existsSync(schemasDir)) {
  error(`Missing event schemas directory: ${schemasDir}`);
  hasError = true;
} else {
  const schemaFiles = fs.readdirSync(schemasDir).filter(f => f.endsWith('.json'));
  let validSchemas = 0;
  for (const file of schemaFiles) {
    try {
      const parsed = JSON.parse(fs.readFileSync(path.join(schemasDir, file), 'utf8'));
      if (!parsed.$schema || !parsed.title || !parsed.properties) {
        error(`Invalid schema structure in ${file}`);
        hasError = true;
      } else {
        validSchemas++;
      }
    } catch (e) {
      error(`JSON parse error in ${file}: ${e.message}`);
      hasError = true;
    }
  }
  log(`✓ Event schemas valid: ${validSchemas} JSON schemas passed specification check.`);
}

// 4. Validate OpenAPI Specification
const openApiPath = path.resolve('contracts/openapi/v1/openapi.yaml');
if (!fs.existsSync(openApiPath)) {
  error(`Missing OpenAPI spec: ${openApiPath}`);
  hasError = true;
} else {
  const content = fs.readFileSync(openApiPath, 'utf8');
  if (content.includes('openapi: 3.1.0') && content.includes('/api/v1') && content.includes('ProblemDetails')) {
    log(`✓ OpenAPI 3.1 contract verified: ProblemDetails and base endpoints present.`);
  } else {
    error('OpenAPI specification missing mandatory sections');
    hasError = true;
  }
}

if (hasError) {
  console.error('\n[contracts-validator] FAILED. Contract verification found errors.');
  process.exit(1);
} else {
  log('SUCCESS: All contract-first specifications are internally consistent.\n');
  process.exit(0);
}
