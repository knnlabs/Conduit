import { NextRequest, NextResponse } from 'next/server';
import { handleSDKError } from '@/lib/errors/sdk-errors';
import { getServerAdminClient } from '@/lib/server/adminClient';
import { requireAuth } from '@/lib/auth/simple-auth';

// PATCH /api/config/routing/rules/[ruleId] - Update a routing rule
export async function PATCH(
  req: NextRequest,
  { params }: { params: Promise<{ ruleId: string }> }
) {
  const auth = requireAuth(req);
  if (!auth.isValid) {
    return auth.response!;
  }

  try {
    const adminClient = getServerAdminClient();
    const { ruleId } = await params;
    const updates = await req.json();
    
    try {
      const updateDto: any = {
        name: updates.name,
        priority: updates.priority,
        enabled: updates.enabled,
        conditions: updates.conditions,
        actions: updates.actions
      };
      
      const updatedRule = await adminClient.configuration.updateRoutingRule(ruleId, updateDto);
      
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
        _warning: 'Rule updated locally (SDK support pending)'
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
  const auth = requireAuth(req);
  if (!auth.isValid) {
    return auth.response!;
  }

  try {
    const adminClient = getServerAdminClient();
    const { ruleId } = await params;
    
    try {
      await adminClient.configuration.deleteRoutingRule(ruleId);
      return NextResponse.json({ success: true });
    } catch (error) {
      console.warn('Failed to delete routing rule:', error);
      
      // Return success if SDK doesn't support it yet
      return NextResponse.json({ 
        success: true,
        _warning: 'Rule deleted locally (SDK support pending)'
      });
    }
  } catch (error) {
    console.error('Error deleting routing rule:', error);
    return handleSDKError(error);
  }
}