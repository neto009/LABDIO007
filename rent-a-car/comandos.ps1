docker tag bff-rent-car-local acrlabneto.azurecr.io/bff-rent-car-local:v1 

docker push acrlabneto.azurecr.io/bff-rent-car-local:v1

az containerapp env create --name bff-rent-car-local --resource-group rg-lab-neto --location eastus2

az containerapp create --name bff-rent-car-local --resource-group rg-lab-neto --image acrlabneto.azurecr.io/bff-rent-car-local:v1 --environment bff-rent-car-local --ingress 'external' --target-port 80 --registry-server acrlabneto.azurecr.io