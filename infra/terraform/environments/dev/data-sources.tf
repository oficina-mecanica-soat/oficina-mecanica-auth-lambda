data "aws_ssm_parameter" "vpc_status" {
  name = var.vpc_status_parameter_name
}

data "aws_ssm_parameter" "vpc_id" {
  name = "${var.vpc_ssm_prefix}/vpc_id"
}

data "aws_ssm_parameter" "private_subnet_ids" {
  name = "${var.vpc_ssm_prefix}/private_subnet_ids"
}

data "aws_ssm_parameter" "rds_status" {
  name = var.rds_status_parameter_name
}

data "aws_ssm_parameter" "rds_security_group_id" {
  name = "${var.rds_ssm_prefix}/security_group_id"
}

data "aws_ssm_parameter" "internal_nlb_security_group_id" {
  count = var.clientes_api_habilitada ? 1 : 0
  name  = "${var.kubernetes_ssm_prefix}/internal_nlb_security_group_id"
}

resource "terraform_data" "vpc_ready" {
  input = data.aws_ssm_parameter.vpc_status.value

  lifecycle {
    precondition {
      condition     = data.aws_ssm_parameter.vpc_status.value == "ready"
      error_message = "A VPC precisa estar pronta no SSM antes do apply da Auth Lambda."
    }
  }
}

resource "terraform_data" "rds_ready" {
  input = data.aws_ssm_parameter.rds_status.value

  lifecycle {
    precondition {
      condition     = data.aws_ssm_parameter.rds_status.value == "ready"
      error_message = "O RDS precisa estar pronto no SSM antes do apply da Auth Lambda."
    }
  }
}
