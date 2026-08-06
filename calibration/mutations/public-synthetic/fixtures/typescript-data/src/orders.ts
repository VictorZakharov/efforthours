import { PrismaClient } from "@prisma/client";

const prisma = new PrismaClient();

export interface Order {
  id: string;
  status: string;
}

export async function listOrders(): Promise<Order[]> {
  return prisma.order.findMany();
}
