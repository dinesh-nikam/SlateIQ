# infra/terraform/modules/compute/main.tf
# AWS ECS Fargate Cluster with Unified Image Role Switching (SLQ-ARCH-015 §7, ADR-063)

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

variable "image_uri" {
  type        = string
  description = "Unified immutable backend container image digest (ADR-063)"
  default     = "123456789012.dkr.ecr.ap-south-1.amazonaws.com/slateiq-backend:latest"
}

locals {
  name_prefix = "slq-${var.environment}-${var.region_code}"
}

# 1. ECS Cluster
resource "aws_ecs_cluster" "main" {
  name = "${local.name_prefix}-ecs-cluster"

  setting {
    name  = "containerInsights"
    value = "enabled"
  }
}

# 2. IAM Execution & Task Roles
resource "aws_iam_role" "task_execution_role" {
  name = "${local.name_prefix}-ecs-execution-role"

  assume_role_policy = jsonencode({
    Version = "2012-10-17"
    Statement = [{
      Action    = "sts:AssumeRole"
      Effect    = "Allow"
      Principal = { Service = "ecs-tasks.amazonaws.com" }
    }]
  })
}

resource "aws_iam_role_policy_attachment" "task_execution_policy" {
  role       = aws_iam_role.task_execution_role.name
  policy_arn = "arn:aws:iam::aws:policy/service-role/AmazonECSTaskExecutionRolePolicy"
}

# Task Definition: API Profile (Internet-Facing)
resource "aws_ecs_task_definition" "api" {
  family                   = "${local.name_prefix}-api"
  network_mode             = "awsvpc"
  requires_compatibilities = ["FARGATE"]
  cpu                      = "1024"
  memory                   = "2048"
  execution_role_arn       = aws_iam_role.task_execution_role.arn

  container_definitions = jsonencode([{
    name      = "slateiq-api"
    image     = var.image_uri
    essential = true
    environment = [
      { name = "SLATEIQ_HOST__ROLE", value = "api" },
      { name = "ASPNETCORE_ENVIRONMENT", value = var.environment }
    ]
    portMappings = [{
      containerPort = 5000
      hostPort      = 5000
    }]
    logConfiguration = {
      logDriver = "awslogs"
      options = {
        "awslogs-group"         = "/ecs/${local.name_prefix}-api"
        "awslogs-region"        = "ap-south-1"
        "awslogs-stream-prefix" = "api"
      }
    }
  }])
}

# Task Definition: Outbox Worker Profile (Background Asynchronous)
resource "aws_ecs_task_definition" "worker" {
  family                   = "${local.name_prefix}-worker"
  network_mode             = "awsvpc"
  requires_compatibilities = ["FARGATE"]
  cpu                      = "512"
  memory                   = "1024"
  execution_role_arn       = aws_iam_role.task_execution_role.arn

  container_definitions = jsonencode([{
    name      = "slateiq-worker"
    image     = var.image_uri
    essential = true
    environment = [
      { name = "SLATEIQ_HOST__ROLE", value = "worker" },
      { name = "ASPNETCORE_ENVIRONMENT", value = var.environment }
    ]
    logConfiguration = {
      logDriver = "awslogs"
      options = {
        "awslogs-group"         = "/ecs/${local.name_prefix}-worker"
        "awslogs-region"        = "ap-south-1"
        "awslogs-stream-prefix" = "worker"
      }
    }
  }])
}

output "cluster_id" {
  value = aws_ecs_cluster.main.id
}
