resource "aws_security_group" "lambda" {
  name        = "${var.function_name}-sg"
  description = "Acesso de saida da Auth Lambda da Oficina Mecanica"
  vpc_id      = data.aws_ssm_parameter.vpc_id.value
  tags        = local.common_tags
}

resource "aws_vpc_security_group_egress_rule" "lambda_sql_server_to_rds" {
  count                        = var.clientes_api_habilitada ? 0 : 1
  description                  = "SQL Server da Auth Lambda para o security group do RDS"
  security_group_id            = aws_security_group.lambda.id
  referenced_security_group_id = data.aws_ssm_parameter.rds_security_group_id.value
  ip_protocol                  = "tcp"
  from_port                    = 1433
  to_port                      = 1433

  depends_on = [terraform_data.rds_ready]
}

resource "aws_vpc_security_group_ingress_rule" "rds_sql_server_from_lambda" {
  count                        = var.clientes_api_habilitada ? 0 : 1
  description                  = "SQL Server do security group da Auth Lambda para o RDS"
  security_group_id            = data.aws_ssm_parameter.rds_security_group_id.value
  referenced_security_group_id = aws_security_group.lambda.id
  ip_protocol                  = "tcp"
  from_port                    = 1433
  to_port                      = 1433

  depends_on = [terraform_data.rds_ready]
}

moved {
  from = aws_vpc_security_group_egress_rule.lambda_sql_server_to_rds
  to   = aws_vpc_security_group_egress_rule.lambda_sql_server_to_rds[0]
}

moved {
  from = aws_vpc_security_group_ingress_rule.rds_sql_server_from_lambda
  to   = aws_vpc_security_group_ingress_rule.rds_sql_server_from_lambda[0]
}

resource "aws_vpc_security_group_egress_rule" "lambda_clientes_para_nlb" {
  count                        = var.clientes_api_habilitada ? 1 : 0
  description                  = "Consulta privada de clientes da Auth Lambda para o NLB"
  security_group_id            = aws_security_group.lambda.id
  referenced_security_group_id = data.aws_ssm_parameter.internal_nlb_security_group_id[0].value
  ip_protocol                  = "tcp"
  from_port                    = 80
  to_port                      = 80
}

resource "aws_vpc_security_group_ingress_rule" "nlb_clientes_da_lambda" {
  count                        = var.clientes_api_habilitada ? 1 : 0
  description                  = "Consulta privada de clientes originada na Auth Lambda"
  security_group_id            = data.aws_ssm_parameter.internal_nlb_security_group_id[0].value
  referenced_security_group_id = aws_security_group.lambda.id
  ip_protocol                  = "tcp"
  from_port                    = 80
  to_port                      = 80
}
