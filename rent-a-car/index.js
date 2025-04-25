const express = require('express');
const cors = require('cors');
const {DefaultAzureCredential} = require('@azure/identity');
const {ServiceBusClient} = require('@azure/service-bus');
require('dotenv').config();

const app = express();
app.use(cors());
app.use(express.json());

app.post('/api/rent-a-car', async (req, res) => {
    const {nome, email, modelo, ano, tempoAluguel} = req.body;
    const connectionString = "";

    const mensagem = {
        nome,
        email,
        modelo,
        ano,
        tempoAluguel,
        data: new Date().toISOString(),
    };

    try {
        const serviceBusConnection = connectionString;
        const queueName = "rent-a-car-queue";
        const sbClient = new ServiceBusClient(serviceBusConnection);
        const sender = sbClient.createSender(queueName);
        const message = {
            body: JSON.stringify(mensagem),
            contentType: "application/json",
            label: "rent-a-car",
        };

        await sender.sendMessages(message);
        await sender.close();
        await sbClient.close();

        res.status(200).json({message: "Mensagem enviada com sucesso!"});
    } catch (error) {
        console.error("Error sending message:", error);
        res.status(500).json({message: "Erro ao enviar mensagem."});
    }
})

app.listen(3000, () => {
    console.log("Servidor rodando na porta 3000");
});