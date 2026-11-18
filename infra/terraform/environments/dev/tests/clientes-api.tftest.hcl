mock_provider "aws" {
  mock_data "aws_ssm_parameter" {
    defaults = { value = "ready" }
  }
  mock_data "aws_iam_role" {
    defaults = { arn = "arn:aws:iam::123456789012:role/LabRole" }
  }
}

variables {
  lambda_execution_role_name = "LabRole"
  lambda_package_file_path   = "pacote-nao-utilizado-em-plan.zip"
  connection_string          = "conexao-ficticia-somente-teste"
  jwt_secret                 = "segredo-ficticio-de-teste-com-mais-de-32-caracteres"
}

run "modo_legado_preservado_sem_ativacao" {
  command = plan
  assert {
    condition = (length(aws_vpc_security_group_egress_rule.lambda_sql_server_to_rds) == 1
      && length(aws_vpc_security_group_ingress_rule.rds_sql_server_from_lambda) == 1
      && length(aws_vpc_security_group_egress_rule.lambda_clientes_para_nlb) == 0
      && aws_lambda_function.auth.environment[0].variables["ClientesApi__Habilitado"] == "false"
      && contains(keys(aws_lambda_function.auth.environment[0].variables), "ConnectionStrings__SqlServer")
    && !contains(keys(aws_lambda_function.auth.environment[0].variables), "ClientesApi__Token"))
    error_message = "O merge não deve ativar o corte nem introduzir dependência privada no modo atual."
  }
}

run "modo_privado_sem_credencial_ou_rede_sql" {
  command = plan
  variables {
    clientes_api_habilitada    = true
    clientes_api_endereco_base = "http://internal-teste.elb.us-east-1.amazonaws.com"
    clientes_api_token         = "credencial-tecnica-ficticia-somente-teste"
  }
  assert {
    condition = (length(aws_vpc_security_group_egress_rule.lambda_sql_server_to_rds) == 0
      && length(aws_vpc_security_group_ingress_rule.rds_sql_server_from_lambda) == 0
      && length(aws_vpc_security_group_egress_rule.lambda_clientes_para_nlb) == 1
      && length(aws_vpc_security_group_ingress_rule.nlb_clientes_da_lambda) == 1
      && aws_vpc_security_group_egress_rule.lambda_clientes_para_nlb[0].from_port == 80
      && aws_vpc_security_group_ingress_rule.nlb_clientes_da_lambda[0].to_port == 80
      && aws_lambda_function.auth.environment[0].variables["ClientesApi__Habilitado"] == "true"
      && contains(keys(aws_lambda_function.auth.environment[0].variables), "ClientesApi__Token")
    && !contains(keys(aws_lambda_function.auth.environment[0].variables), "ConnectionStrings__SqlServer"))
    error_message = "O modo privado deve usar a porta do NLB e retirar o acesso SQL da Lambda."
  }
}

run "credencial_privada_invalida_bloqueia" {
  command = plan
  variables {
    clientes_api_habilitada    = true
    clientes_api_endereco_base = "http://internal-teste.elb.us-east-1.amazonaws.com"
    clientes_api_token         = "curta"
  }
  expect_failures = [var.clientes_api_token]
}
