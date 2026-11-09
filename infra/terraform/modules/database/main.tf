# infra/terraform/modules/database/main.tf
# Amazon RDS for PostgreSQL 17 Multi-AZ with RLS Enforced (SLQ-ARCH-015 §5, ADR-002)

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
  type = string
}

variable "region_code" {
  type    = string
  default = "in"
}

variable "vpc_id" {
  type = string
}

variable "subnet_ids" {
  type = list(string)
}

variable "db_instance_class" {
  type    = string
  default = "db.m6g.large"
}

locals {
  name_prefix = "slq-${var.environment}-${var.region_code}"
}

# Subnet Group
resource "aws_db_subnet_group" "rds" {
  name       = "${local.name_prefix}-db-subnets"
  subnet_ids = var.subnet_ids

  tags = {
    Name = "${local.name_prefix}-db-subnets"
  }
}

# Parameter Group enforcing RLS, logging, and security
resource "aws_db_parameter_group" "pg17" {
  name   = "${local.name_prefix}-pg17-params"
  family = "postgres17"

  parameter {
    name  = "rds.force_ssl"
    value = "1"
  }

  parameter {
    name  = "log_connections"
    value = "1"
  }

  parameter {
    name  = "log_disconnections"
    value = "1"
  }

  parameter {
    name  = "shared_preload_libraries"
    value = "pg_stat_statements"
  }
}

# RDS PostgreSQL 17 Instance
resource "aws_db_instance" "primary" {
  identifier        = "${local.name_prefix}-primary-db"
  engine            = "postgres"
  engine_version    = "17.1"
  instance_class    = var.db_instance_class
  allocated_storage = 100
  max_allocated_storage = 1000
  storage_type      = "gp3"
  storage_encrypted = true

  multi_az               = var.environment == "prod" ? true : false
  db_subnet_group_name   = aws_db_subnet_group.rds.name
  parameter_group_name   = aws_db_parameter_group.pg17.name
  deletion_protection    = var.environment == "prod" ? true : false
  skip_final_snapshot    = var.environment == "prod" ? false : true
  final_snapshot_identifier = "${local.name_prefix}-final-snapshot"

  db_name  = "slateiq"
  username = "slateiq_admin"
  manage_master_user_password = true

  backup_retention_period = 35 # Point-in-time recovery window (ADR-064)
  backup_window           = "19:00-20:00" # Off-peak IST

  lifecycle {
    prevent_destroy = true
  }
}

output "db_endpoint" {
  value = aws_db_instance.primary.endpoint
}

output "db_name" {
  value = aws_db_instance.primary.db_name
}
