# infra/terraform/modules/storage/main.tf
# S3 Object Storage with Scan-Before-Serve and Object Lock (SLQ-ARCH-015 §6, ADR-006, ADR-046)

terraform {
  required_version = ">= 1.9.0"
  required_providers {
    aws = {
      source  = "hashicorp/aws"
      version = "~> 5.75"
    }
  }
}

variable "environment" {
  type        = string
  description = "Environment name (dev, test, staging, prod)"
}

variable "region_code" {
  type        = string
  description = "Region code (in, eu)"
  default     = "in"
}

locals {
  name_prefix = "slq-${var.environment}-${var.region_code}"
}

# 1. Quarantined Uploads Bucket (Pre-Scan Landing Zone)
resource "aws_s3_bucket" "quarantine" {
  bucket = "${local.name_prefix}-evidence-quarantine"

  lifecycle {
    prevent_destroy = true
  }
}

resource "aws_s3_bucket_public_access_block" "quarantine" {
  bucket = aws_s3_bucket.quarantine.id

  block_public_acls       = true
  block_public_policy     = true
  ignore_public_acls      = true
  restrict_public_buckets = true
}

resource "aws_s3_bucket_server_side_encryption_configuration" "quarantine" {
  bucket = aws_s3_bucket.quarantine.id

  rule {
    apply_server_side_encryption_by_default {
      sse_algorithm = "AES256"
    }
  }
}

# 2. Approved Evidence Bucket (Post-Scan Clean Documents) with Object Lock
resource "aws_s3_bucket" "evidence" {
  bucket = "${local.name_prefix}-evidence"

  object_lock_enabled = true

  lifecycle {
    prevent_destroy = true
  }
}

resource "aws_s3_bucket_public_access_block" "evidence" {
  bucket = aws_s3_bucket.evidence.id

  block_public_acls       = true
  block_public_policy     = true
  ignore_public_acls      = true
  restrict_public_buckets = true
}

resource "aws_s3_bucket_versioning" "evidence" {
  bucket = aws_s3_bucket.evidence.id
  versioning_configuration {
    status = "Enabled"
  }
}

# 3. Excel Imports Staging Bucket
resource "aws_s3_bucket" "imports" {
  bucket = "${local.name_prefix}-imports"
}

resource "aws_s3_bucket_public_access_block" "imports" {
  bucket = aws_s3_bucket.imports.id

  block_public_acls       = true
  block_public_policy     = true
  ignore_public_acls      = true
  restrict_public_buckets = true
}

# 4. Long-Term Immutable Audit Archive Bucket
resource "aws_s3_bucket" "audit_archive" {
  bucket = "${local.name_prefix}-audit-archive"

  object_lock_enabled = true

  lifecycle {
    prevent_destroy = true
  }
}

resource "aws_s3_bucket_public_access_block" "audit_archive" {
  bucket = aws_s3_bucket.audit_archive.id

  block_public_acls       = true
  block_public_policy     = true
  ignore_public_acls      = true
  restrict_public_buckets = true
}

output "quarantine_bucket_name" {
  value = aws_s3_bucket.quarantine.id
}

output "evidence_bucket_name" {
  value = aws_s3_bucket.evidence.id
}

output "imports_bucket_name" {
  value = aws_s3_bucket.imports.id
}

output "audit_archive_bucket_name" {
  value = aws_s3_bucket.audit_archive.id
}
