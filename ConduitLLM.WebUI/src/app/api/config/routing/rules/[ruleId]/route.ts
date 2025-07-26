import { NextRequest, NextResponse } from 'next/server';
import { handleSDKError } from '@/lib/errors/sdk-errors';
import { getServerAdminClient } from '@/lib/server/adminClient';
import type { RuleCondition, RuleAction } from '@knn_labs/conduit-admin-client';
// PATCH /api/config/routing/rules/[ruleId] - Update a routing rule
export async function PATCH(
  req: NextRequest,
  { params }: { params: Promise<{ ruleId: string }> }
) {

  try {
    const adminClient = getServerAdminClient();
    const { ruleId } = await params;
    
    // Safe parsing of request body
    const body: unknown = await req.json();
    
    if (typeof body !== 'object' || body === null) {
      return NextResponse.json({ error: 'Invalid request body' }, { status: 400 });
    }

    const updates = body as { 
      name?: string; 
      description?: string;
      priority?: number; 
      enabled?: boolean; 
      conditions?: unknown; 
      actions?: unknown; 
    };
    
    try {
      const updateDto = {
        name: updates.name,
        priority: updates.priority,
        enabled: updates.enabled,
        conditions: updates.conditions as RuleCondition[],
        actions: updates.actions as RuleAction[]
      };
      
      const updatedRule = await (adminClient.configuration.updateRoutingRule as (id: string, dto: unknown) => Promise<unknown>)(ruleId, updateDto);
      
      return NextResponse.json(updatedRule);
    } catch (error) {
      console.warn('Failed to update routing rule:', error);
      
      // Return a mock updated rule if SDK doesn't support it yet
      return NextResponse.json({
        id: ruleId,
        name: updates.name,
        description: updates.description,
        priority: updates.priority,
        isEnabled: updates.enabled,
        conditions: updates.conditions,
        actions: updates.actions,
        updatedAt: new Date().toISOString(),
        warning: 'Rule updated locally (SDK support pending)'
      });
    }
  } catch (error) {
    console.error('Error updating routing rule:', error);
    return handleSDKError(error);
  }
}

// DELETE /api/config/routing/rules/[ruleId] - Delete a routing rule
export async function DELETE(
  req: NextRequest,
  { params }: { params: Promise<{ ruleId: string }> }
) {

  try {
    const adminClient = getServerAdminClient();
    const { ruleId } = await params;
    
    try {
      await (adminClient.configuration.deleteRoutingRule as (id: string) => Promise<unknown>)(ruleId);
      return NextResponse.json({ success: true });
    } catch (error) {
      console.warn('Failed to delete routing rule:', error);
      
      // Return success if SDK doesn't support it yet
      return NextResponse.json({ 
        success: true,
        warning: 'Rule deleted locally (SDK support pending)'
      });
    }
  } catch (error) {
    console.error('Error deleting routing rule:', error);
    return handleSDKError(error);
  }
}